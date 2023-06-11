/* eslint-disable no-proto */
/* eslint-disable accessor-pairs */
/* eslint-disable no-global-assign */

/* wsHook.js
 * https://github.com/skepticfx/wshook
 * Reference: http://www.w3.org/TR/2011/WD-websockets-20110419/#websocket
 * 
 * Reserved Names:
 * ___wsHook___
 * ___originalWS___
 * ___wsFilter___
 * 
 * Force transform:
 * _addEventListener
 */
var ___wsHook___ = {};
(function () {
    // Mutable MessageEvent.
    // Subclasses MessageEvent and makes data, origin and other MessageEvent properites mutatble.
    function MutableMessageEvent(o) {
        this.bubbles = o.bubbles || false
        this.cancelBubble = o.cancelBubble || false
        this.cancelable = o.cancelable || false
        this.currentTarget = o.currentTarget || null
        this.data = o.data || null
        this.defaultPrevented = o.defaultPrevented || false
        this.eventPhase = o.eventPhase || 0
        this.lastEventId = o.lastEventId || ''
        this.origin = o.origin || ''
        this.path = o.path || new Array(0)
        this.ports = o.parts || new Array(0)
        this.returnValue = o.returnValue || true
        this.source = o.source || null
        this.srcElement = o.srcElement || null
        this.target = o.target || null
        this.timeStamp = o.timeStamp || null
        this.type = o.type || 'message'
        this.__proto__ = o.__proto__ || MessageEvent.__proto__
    }

    var before = ___wsHook___.before = function (data, modJson, wsObject) {
        return data
    }
    var after = ___wsHook___.after = function (e, modJson, wsObject) {
        return e
    }
    var modifyUrl = ___wsHook___.modifyUrl = function (url) {
        return url
    }
    ___wsHook___.resetHooks = function () {
        ___wsHook___.before = before
        ___wsHook___.after = after
        ___wsHook___.modifyUrl = modifyUrl
    }

    window['___wsFilter___'] = {
        'undefined': function (d) { return false; },
        'null': function (d) { return false; },
        'active': false
    };
    //example: ___wsFilter___['welcome'] = function(data){return true;}

    function checkFilter(data) {
        let filterActive = window['___wsFilter___'].active;
        let json = filterActive ? JSON.parse(data) : null;
        let filter = filterActive ? window['___wsFilter___'][json.type] : null;
        let filtered = (filter && typeof (filter) === 'function') ? filter(json) : null;
        if (!filterActive || filtered)
            return filtered === true ? null : filtered; // filtered==true -> pass-thru
        else
            return undefined
    }

    let _WS = window.WebSocket
    window['___originalWS___'] = _WS;
    let newSend = function (self, _send) {
        return function (data) {
            let filtered = checkFilter(data);
            if (filtered !== undefined)
                arguments[0] = ___wsHook___.before(data, filtered, self)
            _send.apply(this, arguments)
        }
    }
    let newAddEventListener = function (self, _addEventListener) {
        return function () {
            let eventThis = this
            if (arguments[0] === 'message') {
                arguments[1] = (function (userFunc) {
                    return function instrumentAddEventListener() {
                        let filtered = checkFilter(arguments[0].data);
                        if (filtered !== undefined)
                            arguments[0] = ___wsHook___.after(new MutableMessageEvent(arguments[0]), filtered, self)
                        if (arguments[0] === null) return
                        userFunc.apply(eventThis, arguments)
                    }
                })(arguments[1])
            }
            return _addEventListener.apply(this, arguments)
        }
    }
    window.WebSocket = function (url, protocols) {
        let WSObject
        url = ___wsHook___.modifyUrl(url) || url
        this.url = url
        this.protocols = protocols
        if (!this.protocols) { WSObject = new _WS(url) } else { WSObject = new _WS(url, protocols) }

        let _send = WSObject.send
        WSObject.send = newSend(WSObject, _send)

        // Events needs to be proxied and bubbled down.
        let _addEventListener = WSObject._addEventListener = WSObject.addEventListener
        WSObject.addEventListener = newAddEventListener(WSObject, _addEventListener)

        Object.defineProperty(WSObject, 'onmessage', {
            configurable: true,
            enumerable: true,
            'set': function () {
                let eventThis = this
                let userFunc = arguments[0]
                let onMessageHandler = function () {
                    let filtered = checkFilter(arguments[0].data);
                    if (filtered !== undefined) {
                        arguments[0] = ___wsHook___.after(new MutableMessageEvent(arguments[0]), filtered, WSObject)
                    }
                    if (arguments[0] === null) return
                    userFunc.apply(eventThis, arguments)
                }
                WSObject._addEventListener.apply(this, ['message', onMessageHandler, false])
            }
        })

        return WSObject
    }

    // Overwrite default prototype
    window.WebSocket.prototype = _WS.prototype

    // Overwrite default prototype functions
    let _send = window.WebSocket.prototype.send;
    window.WebSocket.prototype.send = newSend(window.WebSocket.prototype, _send);

    let _addEventListener = window.WebSocket.prototype.__proto__.addEventListener
    window.WebSocket.prototype.addEventListener = newAddEventListener(window.WebSocket.prototype, _addEventListener);
})();
