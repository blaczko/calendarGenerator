/**
 * Emulates google.script.run for Cloudflare Pages.
 * Browser → POST /api → Apps Script doPost JSON API.
 */
(function (global) {
  var apiBase = (global.APP_CONFIG && global.APP_CONFIG.apiBase) || '/api';

  var ACTIONS = [
    'getPublicConfig',
    'createOrder',
    'submitOrder',
    'getOrderByToken',
    'getOrderReviewByToken',
    'getOrderReviewByOrderId',
    'updateOrder',
    'updateOrderByOrderId',
    'finalizeOrder',
    'uploadPhoto',
    'uploadPhotosBatch',
    'uploadPhotosBatchByOrderId',
    'deletePhoto',
    'deletePhotoByOrderId',
    'adminLogin',
    'adminListOrders',
    'adminSetStatus'
  ];

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
    var api = {};

    api.withSuccessHandler = function (fn) {
      successHandler = fn;
      return api;
    };
    api.withFailureHandler = function (fn) {
      failureHandler = fn;
      return api;
    };

    ACTIONS.forEach(function (name) {
      api[name] = function () {
        var args = Array.prototype.slice.call(arguments);
        callAction(name, args)
          .then(function (result) {
            if (successHandler) successHandler(result);
          })
          .catch(function (err) {
            var error = err instanceof Error ? err : new Error(String(err));
            if (failureHandler) failureHandler(error);
            else console.error(error);
          });
      };
    });

    return api;
  }

  global.google = global.google || {};
  global.google.script = global.google.script || {};
  Object.defineProperty(global.google.script, 'run', {
    configurable: true,
    get: function () {
      return createRunner();
    }
  });

  global.NaptarApi = { callAction: callAction, apiBase: apiBase, actions: ACTIONS };
})(window);
