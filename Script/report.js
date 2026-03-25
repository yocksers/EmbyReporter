(function () {
    'use strict';

    var PATCH_FLAG = '_embyReporterPatched';

    function patchActionSheet(actionSheet, connectionManager) {
        if (actionSheet[PATCH_FLAG]) return;
        actionSheet[PATCH_FLAG] = true;

        var originalShow = actionSheet.show;

        actionSheet.show = function (options) {
            var item = options.item;
            var isVideo = item &&
                (item.Type === 'Movie' || item.Type === 'Episode' || item.Type === 'Video');

            if (isVideo && Array.isArray(options.items)) {
                options.items = options.items.concat([{
                    Name: 'Report Issue',
                    Id: 'reportIssue',
                    icon: 'sync_problem'
                }]);
            }

            return originalShow(options).then(
                function (result) {
                    if (result === 'reportIssue' && item) {
                        var description = prompt(
                            'Describe the playback problem for "' + (item.Name || 'this item') + '":',
                            ''
                        );
                        if (description !== null && description.trim() !== '') {
                            var apiClient = connectionManager.getApiClient(item);
                            apiClient.ajax({
                                type: 'POST',
                                url: apiClient.getUrl('/EmbyReporter/ReportIssue'),
                                contentType: 'application/json',
                                data: JSON.stringify({
                                    ItemId: item.Id,
                                    ItemName: item.Name,
                                    Description: description
                                })
                            }).then(function () {
                                alert('Playback issue reported successfully!');
                            }).catch(function () {
                                alert('Failed to report issue. Please try again.');
                            });
                        }
                    }
                    return result;
                },
                function (err) {
                    return Promise.reject(err);
                }
            );
        };
    }

    function tryPatch() {
        if (typeof require !== 'function') {
            setTimeout(tryPatch, 500);
            return;
        }

        require(
            ['modules/actionsheet/actionsheet', 'emby-apiclient/connectionmanager'],
            function (actionSheetModule, connectionManagerModule) {
                var actionSheet = actionSheetModule &&
                    (actionSheetModule.default || actionSheetModule);
                var connectionManager = connectionManagerModule &&
                    (connectionManagerModule.default || connectionManagerModule);

                if (actionSheet && connectionManager &&
                        typeof actionSheet.show === 'function') {
                    patchActionSheet(actionSheet, connectionManager);
                } else {
                    console.warn('[EmbyReporter] actionsheet or connectionmanager not available.');
                }
            },
            function (err) {
                console.warn('[EmbyReporter] Could not load required modules:', err && err.requireModules);
            }
        );
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', tryPatch);
    } else {
        tryPatch();
    }
}());
