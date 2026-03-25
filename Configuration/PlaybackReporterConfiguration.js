define(['baseView', 'loading', 'toast', 'emby-input', 'emby-button', 'emby-checkbox'], function (BaseView, loading, toast) {
    'use strict';

    const pluginId = "decab536-f5ca-4810-88c2-0e60f652b921";

    function getPluginConfiguration() {
        return ApiClient.getPluginConfiguration(pluginId);
    }

    function updatePluginConfiguration(config) {
        return ApiClient.updatePluginConfiguration(pluginId, config);
    }

    function getLogEvents() {
        return ApiClient.getJSON(ApiClient.getUrl("EmbyReporter/GetIssueReports"));
    }

    function clearLogs() {
        return ApiClient.ajax({ type: "POST", url: ApiClient.getUrl("EmbyReporter/ClearIssueReports") });
    }

    function renderLogs(view) {
        const container = view.querySelector('#logContainer');
        getLogEvents().then(events => {
            if (events.length === 0) {
                container.innerHTML = '<p>No playback issues have been reported yet.</p>';
                return;
            }

            const html = events.map(entry => {
                const eventDate = new Date(entry.Timestamp).toLocaleString();
                const icon = 'sync_problem';
                const itemName = entry.ItemName || 'Unknown Item';
                const description = entry.Description || '';
                const libraryName = entry.LibraryName || '';
                const path = entry.Path || '';
                const userAndClient = `${entry.Username || 'Unknown User'} on ${entry.ClientName || 'Unknown Client'}`;

                return `
                <div class="listItem" style="display:flex; align-items: center; padding: 0.8em 0;">
                     <i class="md-icon" style="color:#52B54B; margin-right: 1em;">${icon}</i>
                     <div class="listItemBody">
                         <h3 class="listItemTitle">${itemName}</h3>
                        ${description ? `<div class="listItemText"><strong>Issue:</strong> ${description}</div>` : ''}
                        ${libraryName ? `<div class="listItemText"><strong>Library:</strong> ${libraryName}</div>` : ''}
                        ${path ? `<div class="listItemText secondary" style="word-break: break-all;"><strong>File:</strong> ${path}</div>` : ''}
                        <div class="listItemText secondary" style="margin-top: 0.5em;">${userAndClient} &bull; ${eventDate}</div>
                     </div>
                </div>
            `;
            }).join('');
            container.innerHTML = html;
        });
    }

    return class extends BaseView {
        constructor(view, params) {
            super(view, params);

            view.querySelector('.playbackReporterForm').addEventListener('submit', (e) => {
                e.preventDefault();
                this.saveData(view);
                return false;
            });

            view.querySelector('#btnClearLog').addEventListener('click', () => {
                clearLogs().then(() => {
                    toast('Issue reports have been cleared.');
                    renderLogs(view);
                });
            });

            const statusMsg = view.querySelector('#scriptStatusMessage');

            function showScriptStatus(success, message) {
                statusMsg.textContent = message;
                statusMsg.style.color = success ? '#52B54B' : '#cc0000';
                statusMsg.style.display = 'block';
            }

            view.querySelector('#btnInjectScript').addEventListener('click', () => {
                loading.show();
                ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('EmbyReporter/InjectScript'), dataType: 'json' })
                    .then(result => {
                        loading.hide();
                        showScriptStatus(result.Success, result.Message);
                    }).catch(() => {
                        loading.hide();
                        showScriptStatus(false, 'An unexpected error occurred. Check the Emby server log.');
                    });
            });

            view.querySelector('#btnRemoveScript').addEventListener('click', () => {
                loading.show();
                ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('EmbyReporter/RemoveScript'), dataType: 'json' })
                    .then(result => {
                        loading.hide();
                        showScriptStatus(result.Success, result.Message);
                    }).catch(() => {
                        loading.hide();
                        showScriptStatus(false, 'An unexpected error occurred. Check the Emby server log.');
                    });
            });
        }

        loadData(view) {
            loading.show();
            getPluginConfiguration().then(config => {
                this.config = config;

                view.querySelector('#chkEnablePlaybackIssueNotifications').checked = config.EnablePlaybackIssueNotifications;

                const bookmarkletCode = `javascript:(function(){if(typeof ApiClient==='undefined'){alert('This bookmarklet must be run from within the Emby web interface.');return;}function getItemIdFromUrl(){var hash=window.location.hash||'';var match=hash.match(/id=([^&]*)/);if(match&&match[1])return match[1];return null;}function guessItemName(){var selectors=['.playerTitle','.item-title','.title','h1.title','meta[property="og:title"]'];for(var i=0;i<selectors.length;i++){try{var sel=selectors[i];if(sel.indexOf('meta[')===0){var m=document.querySelector(sel);if(m&&m.content)return m.content;}else{var el=document.querySelector(sel);if(el&&el.textContent)return el.textContent.trim();}}catch(e){}}return '';}var itemId=getItemIdFromUrl();if(!itemId){alert('Could not find an Item ID on this page. Please navigate to the details page of a movie or episode before using this.');return;}var itemName=guessItemName();var description=prompt('Please describe the playback problem (e.g. audio out of sync, video freezes, etc.):','');if(description===null)return;var endpoint='/EmbyReporter/ReportIssue';ApiClient.ajax({type:'POST',url:ApiClient.getUrl(endpoint),contentType:'application/json',data:JSON.stringify({ItemId:itemId,ItemName:itemName,Description:description})}).then(function(){alert('Playback issue reported successfully!');}).catch(function(error){console.error('Report Issue bookmarklet error:',error);var errorText='Failed to report issue.';if(error&&error.status&&error.statusText){errorText='Failed to report issue: '+error.status+' '+error.statusText;}alert(errorText);});})();`;
                view.querySelector('#we-bookmarklet-text').value = bookmarkletCode;

                renderLogs(view);
                loading.hide();
            });
        }

        saveData(view) {
            loading.show();
            this.config.EnablePlaybackIssueNotifications = view.querySelector('#chkEnablePlaybackIssueNotifications').checked;

            updatePluginConfiguration(this.config).then(result => {
                loading.hide();
                Dashboard.processPluginConfigurationUpdateResult(result);
                toast('Configuration saved.');
            }).catch(() => {
                loading.hide();
                toast({ type: 'error', text: 'Error saving configuration.' });
            });
        }

        onResume(options) {
            super.onResume(options);
            this.loadData(this.view);
        }
    };
});