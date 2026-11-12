/**
 * Emulates google.script.run for a static frontend hosted on Cloudflare Pages.
 * Calls same-origin /api which proxies to Apps Script doPost JSON API.
 */
(function (global) {
  var apiBase = (global.APP_CONFIG && global.APP_CONFIG.apiBase) || '/api';

  function callAction(action, args) {
    return fetch(apiBase, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ action: action, args: args || [] })
    }).then(function (res) {
      return res.text().then(function (text) {
        var data;
        try {
          data = JSON.parse(text);
        } catch (e) {
          throw new Error(
            'Érvénytelen API válasz (HTTP ' + res.status + '). ' +
            'Ellenőrizze a Cloudflare GAS_EXEC_URL beállítást.'
          );
        }
        if (!res.ok || !data || data.ok === false) {
          throw new Error((data && data.error) || ('API hiba (HTTP ' + res.status + ')'));
        }
        return data.result;
      });
    });
  }

  function createRunner() {
    var successHandler = null;
    var failureHandler = null;

    function fail(err) {
      var error = err instanceof Error ? err : new Error(String(err));
      if (failureHandler) failureHandler(error);
      else console.error(error);
    }

    var api = {
      withSuccessHandler: function (fn) {
        successHandler = fn;
        return api;
      },
      withFailureHandler: function (fn) {
        failureHandler = fn;
        return api;
      }
    };

    return new Proxy(api, {
      get: function (target, prop) {
        if (prop in target) return target[prop];
        if (typeof prop !== 'string' || prop.startsWith('_')) return undefined;
        return function () {
          var args = Array.prototype.slice.call(arguments);
          callAction(prop, args)
            .then(function (result) {
              if (successHandler) successHandler(result);
            })
            .catch(fail);
        };
      }
    });
  }

  global.google = global.google || {};
  global.google.script = global.google.script || {};
  Object.defineProperty(global.google.script, 'run', {
    configurable: true,
    get: function () {
      return createRunner();
    }
  });

  global.NaptarApi = { callAction: callAction, apiBase: apiBase };
})(window);
