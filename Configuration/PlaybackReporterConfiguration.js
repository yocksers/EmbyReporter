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

    function escapeHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    const statusLabels = {
        'Open': 'Open',
        'Acknowledged': 'Acknowledged',
        'WorkingOnIt': 'Working on it',
        'Fixed': 'Marked as Fixed',
        'AwaitingUserResponse': 'Awaiting Response',
        'Confirmed': 'Confirmed Fixed',
        'StillBroken': 'Still Not Working'
    };

    const statusColors = {
        'Open': '#666',
        'Acknowledged': '#4a8abf',
        'WorkingOnIt': '#e67e22',
        'Fixed': '#52B54B',
        'AwaitingUserResponse': '#4a8abf',
        'Confirmed': '#52B54B',
        'StillBroken': '#c0392b'
    };

    function renderLogs(view) {
        const container = view.querySelector('#logContainer');

        const openReplies = {};
        container.querySelectorAll('.er-reply-area').forEach(area => {
            if (area.style.display !== 'none' && area.style.display !== '') {
                const rid = area.getAttribute('data-reportid');
                const ta = area.querySelector('.er-reply-text');
                if (rid) openReplies[rid] = { text: ta ? ta.value : '', focused: ta === document.activeElement };
            }
        });

        getLogEvents().then(events => {
            if (events.length === 0) {
                container.innerHTML = '<p>No playback issues have been reported yet.</p>';
                return;
            }

            const html = events.map(entry => {
                const eventDate = new Date(entry.Timestamp).toLocaleString();
                const itemName = entry.ItemName || 'Unknown Item';
                const description = entry.Description || '';
                const libraryName = entry.LibraryName || '';
                const path = entry.Path || '';
                const userAndClient = `${entry.Username || 'Unknown User'} on ${entry.ClientName || 'Unknown Client'}`;
                const reportId = entry.ReportId || '';
                const status = entry.Status || 'Open';
                const messages = entry.Messages || [];

                const badgeColor = statusColors[status] || '#666';
                const badgeLabel = statusLabels[status] || status;
                const statusBadge = `<span class="er-status-badge" style="background:${badgeColor};">${badgeLabel}</span>`;

                let chatHtml = '';
                if (messages.length > 0) {
                    chatHtml = '<div class="er-chat-thread">';
                    messages.forEach(msg => {
                        const isSystem = msg.Sender === 'system';
                        const isAdmin = msg.Sender === 'admin';
                        const msgDate = msg.Timestamp ? new Date(msg.Timestamp).toLocaleString() : '';
                        if (isSystem) {
                            chatHtml += `<div class="er-chat-system">`;
                            chatHtml += `${escapeHtml(msg.Text)}${msgDate ? ' &mdash; ' + msgDate : ''}`;
                            chatHtml += '</div>';
                        } else {
                            const bg = isAdmin ? 'rgba(82,181,75,0.12)' : 'rgba(74,138,191,0.12)';
                            const border = isAdmin ? '#52B54B' : '#4a8abf';
                            const label = isAdmin ? 'Admin' : (entry.Username || 'User');
                            chatHtml += `<div class="er-chat-bubble" style="background:${bg};border-left:3px solid ${border};">`;
                            chatHtml += `<div class="er-chat-meta">${escapeHtml(label)}${msgDate ? ' &bull; ' + msgDate : ''}</div>`;
                            chatHtml += `<div class="er-chat-text">${escapeHtml(msg.Text)}</div>`;
                            chatHtml += '</div>';
                        }
                    });
                    chatHtml += '</div>';
                }

                const canReply = reportId && status !== 'Confirmed';

                let replyHtml = '';
                if (canReply) {
                    replyHtml = `
                        <div class="er-reply-area" style="display:none;" data-reportid="${reportId}">
                            <textarea rows="2" class="er-reply-text" placeholder="Type your reply&hellip;"></textarea>
                            <button is="emby-button" type="button" class="raised button-submit er-send-reply" style="margin-right:0.5em;"><span>Send Reply</span></button>
                            <button is="emby-button" type="button" class="raised button-cancel er-cancel-reply"><span>Cancel</span></button>
                        </div>
                        <button is="emby-button" type="button" class="raised er-toggle-reply" data-reportid="${reportId}" style="margin-top:0.6em;font-size:0.85em;"><span>Reply</span></button>`;
                }

                const statusControlHtml = reportId ? `
                    <div style="display:flex;gap:0.6em;align-items:center;flex-wrap:wrap;margin-top:0.7em;">
                        ${canReply ? `
                        <select class="er-status-select" data-reportid="${reportId}" style="border:1px solid rgba(128,128,128,0.4);border-radius:4px;padding:0.3em 0.5em;font-size:0.85em;cursor:pointer;">
                            <option value="">Set status&hellip;</option>
                            <option value="Acknowledged">Acknowledged</option>
                            <option value="WorkingOnIt">Working on it</option>
                            <option value="Fixed">Mark as Fixed</option>
                        </select>
                        <button is="emby-button" type="button" class="raised er-apply-status" data-reportid="${reportId}" style="font-size:0.85em;"><span>Apply</span></button>
                        ` : ''}
                        <button is="emby-button" type="button" class="raised button-cancel er-delete-report" data-reportid="${reportId}" style="font-size:0.85em;"><span>Delete Report</span></button>
                    </div>` : '';

                return `
                <div class="listItem" style="display:flex; align-items: flex-start; padding: 0.8em 0;">
                     <i class="md-icon" style="color:#52B54B; margin-right: 1em; margin-top: 0.25em;">sync_problem</i>
                     <div class="listItemBody" style="flex:1;min-width:0;">
                         <div style="display:flex;align-items:center;gap:0.6em;flex-wrap:wrap;margin-bottom:0.3em;">
                             <h3 class="listItemTitle" style="margin:0;">${itemName}</h3>
                             ${statusBadge}
                         </div>
                        ${description ? `<div class="listItemText"><strong>Issue:</strong> ${escapeHtml(description)}</div>` : ''}
                        ${libraryName ? `<div class="listItemText"><strong>Library:</strong> ${escapeHtml(libraryName)}</div>` : ''}
                        ${path ? `<div class="listItemText secondary" style="word-break:break-all;"><strong>File:</strong> ${escapeHtml(path)}</div>` : ''}
                        <div class="listItemText secondary" style="margin-top:0.5em;">${escapeHtml(userAndClient)} &bull; ${eventDate}</div>
                        ${chatHtml}
                        ${replyHtml}
                        ${statusControlHtml}
                     </div>
                </div>
            `;
            }).join('');
            container.innerHTML = html;

            Object.keys(openReplies).forEach(rid => {
                const area = container.querySelector(`.er-reply-area[data-reportid="${rid}"]`);
                if (!area) return;
                const toggleBtn = container.querySelector(`.er-toggle-reply[data-reportid="${rid}"]`);
                const ta = area.querySelector('.er-reply-text');
                area.style.display = 'block';
                if (toggleBtn) toggleBtn.style.display = 'none';
                if (ta) {
                    ta.value = openReplies[rid].text;
                    if (openReplies[rid].focused) {
                        ta.focus();
                        ta.setSelectionRange(ta.value.length, ta.value.length);
                    }
                }
            });

            container.querySelectorAll('.er-toggle-reply').forEach(btn => {
                btn.addEventListener('click', () => {
                    const listItem = btn.closest('.listItem');
                    const area = listItem ? listItem.querySelector('.er-reply-area') : null;
                    if (area) {
                        const hidden = area.style.display === 'none' || area.style.display === '';
                        area.style.display = hidden ? 'block' : 'none';
                        btn.style.display = hidden ? 'none' : '';
                    }
                });
            });

            container.querySelectorAll('.er-send-reply').forEach(btn => {
                btn.addEventListener('click', () => {
                    const area = btn.closest('.er-reply-area');
                    const reportId = area ? area.getAttribute('data-reportid') : null;
                    const textArea = area ? area.querySelector('.er-reply-text') : null;
                    const message = textArea ? textArea.value.trim() : '';
                    if (!reportId || !message) return;

                    btn.disabled = true;
                    ApiClient.ajax({
                        type: 'POST',
                        url: ApiClient.getUrl('EmbyReporter/Reports/Reply'),
                        contentType: 'application/json',
                        data: JSON.stringify({ ReportId: reportId, Message: message })
                    }).then(() => {
                        toast('Reply sent.');
                        renderLogs(view);
                    }).catch(() => {
                        btn.disabled = false;
                        toast({ type: 'error', text: 'Failed to send reply.' });
                    });
                });
            });

            container.querySelectorAll('.er-cancel-reply').forEach(btn => {
                btn.addEventListener('click', () => {
                    const area = btn.closest('.er-reply-area');
                    const listItem = btn.closest('.listItem');
                    const toggleBtn = listItem ? listItem.querySelector('.er-toggle-reply') : null;
                    if (area) area.style.display = 'none';
                    if (toggleBtn) toggleBtn.style.display = '';
                });
            });

            container.querySelectorAll('.er-apply-status').forEach(btn => {
                btn.addEventListener('click', () => {
                    const listItem = btn.closest('.listItem');
                    const select = listItem ? listItem.querySelector('.er-status-select') : null;
                    const newStatus = select ? select.value : '';
                    const reportId = btn.getAttribute('data-reportid');
                    if (!reportId || !newStatus) return;

                    btn.disabled = true;
                    ApiClient.ajax({
                        type: 'POST',
                        url: ApiClient.getUrl('EmbyReporter/Reports/SetStatus'),
                        contentType: 'application/json',
                        data: JSON.stringify({ ReportId: reportId, Status: newStatus })
                    }).then(() => {
                        toast('Status updated.');
                        renderLogs(view);
                    }).catch(() => {
                        btn.disabled = false;
                        toast({ type: 'error', text: 'Failed to update status.' });
                    });
                });
            });

            container.querySelectorAll('.er-delete-report').forEach(btn => {
                btn.addEventListener('click', () => {
                    const reportId = btn.getAttribute('data-reportid');
                    if (!reportId) return;

                    btn.disabled = true;
                    ApiClient.ajax({
                        type: 'POST',
                        url: ApiClient.getUrl('EmbyReporter/Reports/Delete'),
                        contentType: 'application/json',
                        data: JSON.stringify({ ReportId: reportId })
                    }).then(() => {
                        toast('Report deleted.');
                        renderLogs(view);
                    }).catch(() => {
                        btn.disabled = false;
                        toast({ type: 'error', text: 'Failed to delete report.' });
                    });
                });
            });
        });
    }

    return class extends BaseView {
        constructor(view, params) {
            super(view, params);
            this._pollTimer = null;

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

            // Auto-refresh the report list every 30 seconds while the page is open
            if (this._pollTimer) clearInterval(this._pollTimer);
            this._pollTimer = setInterval(() => renderLogs(view), 5000);
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

        onPause() {
            if (this._pollTimer) {
                clearInterval(this._pollTimer);
                this._pollTimer = null;
            }
            super.onPause && super.onPause();
        }

        onResume(options) {
            super.onResume(options);
            this.loadData(this.view);
        }
    };
});