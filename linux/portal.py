#!/usr/bin/python3
"""Local Wayland portal bridge. One JSON response per stdin command."""
import base64
import json
import os
import sys
import uuid
import gi
gi.require_version('Gst', '1.0')
gi.require_version('GstVideo', '1.0')
from gi.repository import Gio, GLib, Gst, GstVideo

BUS = 'org.freedesktop.portal.Desktop'
PATH = '/org/freedesktop/portal/desktop'
SC = 'org.freedesktop.portal.ScreenCast'
RD = 'org.freedesktop.portal.RemoteDesktop'

class Portal:
    def __init__(self):
        Gst.init(None)
        self.bus = Gio.bus_get_sync(Gio.BusType.SESSION, None)
        self.session = None
        self.pipeline = None
        self.fd = None

    def call(self, interface, method, args):
        return self.bus.call_sync(BUS, PATH, interface, method, args, None,
                                  Gio.DBusCallFlags.NONE, 10000, None)

    def request(self, interface, method, signature, values, options):
        token = 'rp' + uuid.uuid4().hex
        options = dict(options, handle_token=GLib.Variant('s', token))
        sender = self.bus.get_unique_name()[1:].replace('.', '_')
        path = '/org/freedesktop/portal/desktop/request/' + sender + '/' + token
        loop = GLib.MainLoop()
        result = []
        def response(*args):
            result.append(args[-1].unpack())
            loop.quit()
        subscription = self.bus.signal_subscribe(BUS, 'org.freedesktop.portal.Request',
            'Response', path, None, Gio.DBusSignalFlags.NONE, response)
        timeout = GLib.timeout_add_seconds(120, lambda: (loop.quit(), False)[1])
        try:
            self.call(interface, method, GLib.Variant(signature, tuple(values) + (options,)))
            if not result:
                loop.run()
            if not result:
                raise RuntimeError('O seletor do sistema não respondeu em 120 segundos.')
            if result[0][0] != 0:
                raise RuntimeError('Permissão cancelada ou indisponível no portal do sistema.')
            return result[0][1]
        finally:
            self.bus.signal_unsubscribe(subscription)
            if GLib.MainContext.default().find_source_by_id(timeout):
                GLib.source_remove(timeout)

    def open(self):
        self.close()
        try:
            created = self.request(RD, 'CreateSession', '(a{sv})', [],
                {'session_handle_token': GLib.Variant('s', 'rp' + uuid.uuid4().hex)})
            self.session = created['session_handle']
            self.request(RD, 'SelectDevices', '(oa{sv})', [self.session],
                         {'types': GLib.Variant('u', 2)})
            # A monitor stream makes pointer coordinates unambiguous on COSMIC.
            self.request(SC, 'SelectSources', '(oa{sv})', [self.session],
                         {'types': GLib.Variant('u', 1), 'multiple': GLib.Variant('b', False),
                          'cursor_mode': GLib.Variant('u', 1)})
            started = self.request(RD, 'Start', '(osa{sv})', [self.session, ''], {})
            if not started.get('devices', 0) & 2:
                raise RuntimeError('O sistema não autorizou o controle do mouse.')
            self.node, props = started['streams'][0]
            self.logical = props.get('logical_size', props.get('size'))
            reply, descriptors = self.bus.call_with_unix_fd_list_sync(BUS, PATH, SC,
                'OpenPipeWireRemote', GLib.Variant('(oa{sv})', (self.session, {})),
                None, Gio.DBusCallFlags.NONE, 10000, None, None)
            self.fd = descriptors.get(reply.unpack()[0])
            self.pipeline = Gst.parse_launch(
                f'pipewiresrc fd={self.fd} path={self.node} do-timestamp=true ! '
                'videoconvert ! video/x-raw,format=RGB ! '
                'appsink name=frames max-buffers=1 drop=true sync=false')
            self.sink = self.pipeline.get_by_name('frames')
            self.pipeline.set_state(Gst.State.PLAYING)
            return self.frame()
        except Exception:
            self.close()
            raise

    def frame(self):
        sample = self.sink.emit('try-pull-sample', 5 * Gst.SECOND)
        if sample is None:
            raise RuntimeError('Sem imagem do PipeWire. Confira a permissão de compartilhamento.')
        info = GstVideo.VideoInfo.new_from_caps(sample.get_caps())
        buf = sample.get_buffer()
        success, mapped = buf.map(Gst.MapFlags.READ)
        if not success:
            raise RuntimeError('Não foi possível ler a imagem.')
        try:
            stride = info.stride[0]
            offset = info.offset[0]
            data = b''.join(mapped.data[offset+y*stride:offset+y*stride+info.width*3]
                            for y in range(info.height))
        finally:
            buf.unmap(mapped)
        self.width, self.height = info.width, info.height
        return {'width': self.width, 'height': self.height,
                'rgb': base64.b64encode(data).decode('ascii')}

    def scroll(self, x, y, steps):
        logical = self.logical or (self.width, self.height)
        self.call(RD, 'NotifyPointerMotionAbsolute', GLib.Variant('(oa{sv}udd)',
            (self.session, {}, self.node, x*logical[0]/self.width, y*logical[1]/self.height)))
        self.call(RD, 'NotifyPointerAxisDiscrete', GLib.Variant('(oa{sv}ui)',
            (self.session, {}, 0, max(-8, min(8, int(steps))))))
        return {}

    def close(self):
        if self.pipeline:
            self.pipeline.set_state(Gst.State.NULL)
            self.pipeline = None
        if self.fd is not None:
            os.close(self.fd)
            self.fd = None
        if self.session:
            try:
                self.bus.call_sync(BUS, self.session, 'org.freedesktop.portal.Session',
                    'Close', None, None, Gio.DBusCallFlags.NONE, 3000, None)
            except GLib.Error:
                pass
            self.session = None
        return {}

def main():
    portal = Portal()
    try:
        for line in sys.stdin:
            try:
                command = json.loads(line)
                name = command['command']
                if name == 'open': result = portal.open()
                elif name == 'frame': result = portal.frame()
                elif name == 'scroll': result = portal.scroll(command['x'], command['y'], command['steps'])
                elif name == 'close': result = portal.close()
                else: raise ValueError('Comando desconhecido')
                print(json.dumps(dict(ok=True, **result)), flush=True)
            except Exception as error:
                print(json.dumps({'ok': False, 'error': str(error)}), flush=True)
    finally:
        portal.close()

if __name__ == '__main__':
    main()
