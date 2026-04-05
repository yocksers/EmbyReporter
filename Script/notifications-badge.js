(function () {
    'use strict';

    var BADGE_ID = 'emby-reporter-badge';
    var POLL_MS = 30000;

    var _count = 0;
    var _reports = [];
    var _knownAdminMsgCounts = {};
    var _firstPoll = true;
    var _injectTimer = null;
    var _injectAttempts = 0;
    var MAX_INJECT_ATTEMPTS = 40;
    var INJECT_RETRY_MS = 500;
    var _navMenuItem = null;
    var _dialogRefresh = null;

    function getClient() {
        return (typeof window !== 'undefined' && window.ApiClient &&
            typeof window.ApiClient.getUrl === 'function') ? window.ApiClient : null;
    }

    function fetchMessages() {
        var client = getClient();
        if (!client) return;

        client.getJSON(client.getUrl('/EmbyReporter/MyReports')).then(function (reports) {
            _reports = reports || [];
            _count = _reports.length;

            if (!_firstPoll) {
                checkForNewAdminReplies(_reports);
            } else {
                _reports.forEach(function (r) {
                    _knownAdminMsgCounts[r.ReportId] = countAdminMessages(r);
                });
                _firstPoll = false;
            }

            syncBadge();
        }).catch(function () {});
    }

    function countAdminMessages(report) {
        return (report.Messages || []).filter(function (m) {
            return m.Sender === 'admin';
        }).length;
    }

    function checkForNewAdminReplies(reports) {
        var newReplies = [];
        reports.forEach(function (r) {
            var prev = _knownAdminMsgCounts[r.ReportId] || 0;
            var current = countAdminMessages(r);
            if (current > prev) {
                newReplies.push(r.ItemName || 'your report');
            }
            _knownAdminMsgCounts[r.ReportId] = current;
        });

        if (newReplies.length > 0) {
            var msg = newReplies.length === 1
                ? 'Admin replied to: ' + newReplies[0]
                : 'Admin replied to ' + newReplies.length + ' of your reports';
            showToast(msg);
        }
    }

    function showToast(message) {
        if (typeof require === 'function') {
            require(['toast'], function (toast) {
                toast(message);
            });
        }
    }

    function needsResponse() {
        return _reports.some(function (r) {
            return r.Status === 'AwaitingUserResponse' || r.Status === 'Fixed';
        });
    }

    function labelText() {
        var waiting = _reports.filter(function (r) {
            return r.Status === 'AwaitingUserResponse' || r.Status === 'Fixed';
        }).length;
        if (waiting > 0) {
            return waiting === 1 ? '1 reply waiting' : waiting + ' replies waiting';
        }
        return _count === 1 ? '1 active report' : _count + ' active reports';
    }

    function syncNavBadge() {
        if (!_navMenuItem) return;
        var badge = _navMenuItem.querySelector('.er-nav-badge');
        if (_count > 0) {
            if (!badge) {
                badge = document.createElement('span');
                badge.className = 'er-nav-badge';
                badge.style.cssText = 'border-radius:10px;font-size:0.7em;padding:0.1em 0.5em;color:#fff;vertical-align:middle;margin-left:0.4em;display:inline-block;';
                var textSpan = _navMenuItem.querySelector('.navMenuOptionText');
                if (textSpan) textSpan.appendChild(badge);
            }
            badge.textContent = _count;
            badge.style.background = needsResponse() ? '#4a8abf' : '#555555';
            badge.style.display = 'inline-block';
        } else if (badge) {
            badge.style.display = 'none';
        }
    }

    function injectNavMenuItem() {
        if (_navMenuItem && document.body.contains(_navMenuItem)) {
            syncNavBadge();
            return;
        }
        var existing = document.querySelector('.navMenuOption');
        if (!existing) return;
        var container = existing.parentElement;
        if (!container) return;
        var already = container.querySelector('.er-sidebar-btn');
        if (already) {
            _navMenuItem = already;
            syncNavBadge();
            return;
        }
        var a = document.createElement('a');
        a.className = 'navMenuOption er-sidebar-btn';
        a.href = '#';
        a.style.cursor = 'pointer';
        a.innerHTML = '<i class="navMenuOptionIcon md-icon">report_problem</i><span class="navMenuOptionText">Reported Issues</span>';
        a.addEventListener('click', function (e) {
            e.preventDefault();
            openDialog();
        });
        container.appendChild(a);
        _navMenuItem = a;
        syncNavBadge();
    }

    function syncBadge() {
        var existing = document.getElementById(BADGE_ID);

        if (_count === 0) {
            if (existing) existing.style.display = 'none';
        } else if (existing) {
            existing.style.display = '';
            var lbl = existing.querySelector('.erb-label');
            if (lbl) lbl.textContent = labelText();
            var btn = existing.querySelector('button');
            if (btn) btn.style.background = needsResponse() ? '#4a8abf' : '#555555';
        } else {
            scheduleInject(0);
        }

        injectNavMenuItem();
    }

    function scheduleInject(delayMs) {
        if (_injectTimer) clearTimeout(_injectTimer);
        _injectAttempts = 0;
        _injectTimer = setTimeout(attemptInject, delayMs);
    }

    function attemptInject() {
        _injectTimer = null;
        if (_count === 0) return;
        if (document.getElementById(BADGE_ID)) return;

        var anchor = findAnchor();
        if (!anchor) {
            if (_injectAttempts < MAX_INJECT_ATTEMPTS) {
                _injectAttempts++;
                _injectTimer = setTimeout(attemptInject, INJECT_RETRY_MS);
            }
            return;
        }

        _injectAttempts = 0;
        buildBadge(anchor);
    }

    function findAnchor() {
        var container = document.querySelector('.view:not(.hide) .homeSectionsContainer') ||
                        document.querySelector('.homeSectionsContainer');
        if (!container || !document.body.contains(container)) return null;

        var slider = container.querySelector('.scrollSlider');
        if (slider && document.body.contains(slider)) {
            var firstSection = slider.querySelector('.homeSection');
            return { parent: slider, before: firstSection || slider.firstChild };
        }

        return null;
    }

    function buildBadge(anchor) {
        var wrapper = document.createElement('div');
        wrapper.id = BADGE_ID;
        wrapper.style.cssText = 'padding:0.5em 1.7em;';

        var btn = document.createElement('button');
        btn.style.cssText = [
            'display:inline-flex',
            'align-items:center',
            'gap:0.5em',
            'background:' + (needsResponse() ? '#4a8abf' : '#555555'),
            'color:#fff',
            'border:none',
            'border-radius:6px',
            'padding:0.45em 1.1em',
            'font-size:0.9em',
            'cursor:pointer',
            'font-family:inherit'
        ].join(';');

        var icon = document.createElement('span');
        icon.setAttribute('aria-hidden', 'true');
        icon.style.cssText = 'font-size:1em;line-height:1;';
        icon.textContent = '\u26A0';

        var label = document.createElement('span');
        label.className = 'erb-label';
        label.textContent = labelText();

        btn.appendChild(icon);
        btn.appendChild(label);
        btn.addEventListener('click', function (e) {
            e.stopPropagation();
            e.preventDefault();
            openDialog();
        });
        wrapper.appendChild(btn);

        anchor.parent.insertBefore(wrapper, anchor.before);
    }

    var STATUS_LABELS = {
        'Open': 'Open',
        'Acknowledged': 'Acknowledged',
        'WorkingOnIt': 'Working on it',
        'Fixed': 'Marked as Fixed',
        'AwaitingUserResponse': 'Admin replied',
        'Confirmed': 'Confirmed Fixed',
        'StillBroken': 'Still Not Working'
    };
    var STATUS_COLORS = {
        'Open': '#666',
        'Acknowledged': '#4a8abf',
        'WorkingOnIt': '#e67e22',
        'Fixed': '#52B54B',
        'AwaitingUserResponse': '#4a8abf',
        'Confirmed': '#52B54B',
        'StillBroken': '#c0392b'
    };

    function renderReportBlocks(box, reports, refreshCallback) {
        var savedDrafts = {};
        box.querySelectorAll('.erb-report-block').forEach(function (b) {
            var rid = b.getAttribute('data-reportid');
            var ta = b.querySelector('.erb-response-text');
            if (rid && ta && ta.value) savedDrafts[rid] = ta.value;
        });

        box.querySelectorAll('.erb-report-block').forEach(function (b) { b.parentNode.removeChild(b); });

        reports.forEach(function (report) {
            var block = document.createElement('div');
            block.className = 'erb-report-block';
            block.setAttribute('data-reportid', report.ReportId || '');
            block.style.cssText = 'border-top:1px solid #333;padding:1em 0;';

            var title = document.createElement('div');
            title.style.cssText = 'font-weight:bold;color:#ddd;margin-bottom:0.3em;';
            title.textContent = report.ItemName || 'Unknown Item';
            block.appendChild(title);

            var currentStatus = report.Status || 'Open';
            var statusLabel = STATUS_LABELS[currentStatus] || currentStatus;
            var statusColor = STATUS_COLORS[currentStatus] || '#666';

            var statusBadge = document.createElement('span');
            statusBadge.style.cssText = 'display:inline-block;background:' + statusColor +
                ';color:#fff;font-size:0.72em;padding:0.2em 0.6em;border-radius:4px;margin-bottom:0.6em;';
            statusBadge.textContent = statusLabel;
            block.appendChild(statusBadge);

            if (report.Description) {
                var desc = document.createElement('div');
                desc.style.cssText = 'color:#999;font-size:0.85em;margin-bottom:0.7em;font-style:italic;';
                desc.textContent = report.Description;
                block.appendChild(desc);
            }

            (report.Messages || []).forEach(function (msg) {
                var isSystem = msg.Sender === 'system';
                var isAdmin = msg.Sender === 'admin';
                var msgDate = msg.Timestamp ? new Date(msg.Timestamp).toLocaleString() : '';
                var bubble = document.createElement('div');

                if (isSystem) {
                    bubble.style.cssText = 'text-align:center;color:#888;font-size:0.8em;padding:0.3em 0;margin-bottom:0.3em;font-style:italic;';
                    bubble.textContent = msg.Text + (msgDate ? ' \u2014 ' + msgDate : '');
                } else {
                    var bg = isAdmin ? 'rgba(82,181,75,0.15)' : 'rgba(74,138,191,0.15)';
                    var border = isAdmin ? '#52B54B' : '#4a8abf';
                    var senderLabel = isAdmin ? 'Admin' : 'You';
                    bubble.style.cssText = 'background:' + bg + ';border-left:3px solid ' + border +
                        ';padding:0.5em 0.8em;border-radius:0 4px 4px 0;margin-bottom:0.4em;';
                    var meta = document.createElement('div');
                    meta.style.cssText = 'font-size:0.75em;color:#999;margin-bottom:0.15em;';
                    meta.textContent = senderLabel + (msgDate ? ' \u2022 ' + msgDate : '');
                    var text = document.createElement('div');
                    text.style.cssText = 'color:#ddd;word-break:break-word;';
                    text.textContent = msg.Text;
                    bubble.appendChild(meta);
                    bubble.appendChild(text);
                }
                block.appendChild(bubble);
            });

            var needsAction = currentStatus === 'AwaitingUserResponse' || currentStatus === 'Fixed';
            var canComment = currentStatus !== 'Confirmed';

            if (canComment) {
                var textarea = document.createElement('textarea');
                textarea.className = 'erb-response-text';
                textarea.placeholder = 'Write a comment\u2026';
                textarea.rows = 2;
                textarea.style.cssText = [
                    'width:100%',
                    'box-sizing:border-box',
                    'background:rgba(255,255,255,0.07)',
                    'border:1px solid rgba(255,255,255,0.15)',
                    'color:#ddd',
                    'border-radius:4px',
                    'padding:0.5em',
                    'resize:vertical',
                    'font-family:inherit',
                    'font-size:0.9em',
                    'margin:0.8em 0',
                    'display:block'
                ].join(';');
                if (savedDrafts[report.ReportId]) textarea.value = savedDrafts[report.ReportId];
                block.appendChild(textarea);

                var btnRow = document.createElement('div');
                btnRow.style.cssText = 'display:flex;gap:0.7em;flex-wrap:wrap;';

                var sendBtn = document.createElement('button');
                sendBtn.textContent = 'Send Comment';
                sendBtn.style.cssText = 'padding:0.55em 1.2em;border:none;background:#4a8abf;color:#fff;border-radius:4px;cursor:pointer;font-size:0.9em;font-family:inherit;';
                sendBtn.addEventListener('click', function () {
                    var commentText = textarea.value.trim().slice(0, 500);
                    if (!commentText) return;
                    var reportId = block.getAttribute('data-reportid');
                    sendBtn.disabled = true;
                    sendComment(reportId, commentText).then(function () {
                        textarea.value = '';
                        sendBtn.disabled = false;
                        if (refreshCallback) refreshCallback();
                        else fetchMessages();
                    }).catch(function () {
                        sendBtn.disabled = false;
                    });
                });

                if (needsAction) {
                    var confirmBtn = document.createElement('button');
                    confirmBtn.className = 'erb-confirm-btn';
                    confirmBtn.textContent = 'Confirm Fixed - Close Issue';
                    confirmBtn.style.cssText = 'flex:1;padding:0.55em;border:none;background:#52B54B;color:#fff;border-radius:4px;cursor:pointer;font-size:0.9em;font-family:inherit;';
                    confirmBtn.addEventListener('click', function () {
                        var reportId = block.getAttribute('data-reportid');
                        var commentText = textarea.value.trim().slice(0, 500);
                        block.querySelectorAll('button').forEach(function (b) { b.disabled = true; });
                        sendResponse(reportId, true, commentText).then(function () {
                            block.innerHTML = '<div style="text-align:center;color:#52B54B;padding:0.8em 0;">Issue closed. Thank you!</div>';
                            fetchMessages();
                        }).catch(function () {
                            block.querySelectorAll('button').forEach(function (b) { b.disabled = false; });
                        });
                    });
                    btnRow.appendChild(confirmBtn);
                }

                btnRow.appendChild(sendBtn);

                block.appendChild(btnRow);
            }

            box.appendChild(block);
        });
    }

    function openDialog() {
        var overlay = document.createElement('div');
        overlay.style.cssText = [
            'position:fixed',
            'top:0',
            'left:0',
            'right:0',
            'bottom:0',
            'background:rgba(0,0,0,0.8)',
            'z-index:2147483647',
            'display:flex',
            'align-items:flex-start',
            'justify-content:center',
            'padding:2em 1em',
            'box-sizing:border-box',
            'overflow-y:auto',
            'pointer-events:all'
        ].join(';');

        var box = document.createElement('div');
        box.style.cssText = [
            'background:#1c1c1c',
            'border-radius:8px',
            'padding:1.5em 2em',
            'max-width:600px',
            'width:100%',
            'box-sizing:border-box',
            'box-shadow:0 6px 32px rgba(0,0,0,0.7)'
        ].join(';');

        var headerRow = document.createElement('div');
        headerRow.style.cssText = 'display:flex;justify-content:space-between;align-items:center;margin-bottom:1.2em;';

        var heading = document.createElement('h2');
        heading.style.cssText = 'margin:0;color:#fff;font-size:1.15em;';
        heading.textContent = 'Your Report Updates';

        var closeBtn = document.createElement('button');
        closeBtn.style.cssText = 'background:none;border:none;color:#aaa;font-size:1.7em;cursor:pointer;line-height:1;padding:0;';
        closeBtn.innerHTML = '&times;';
        closeBtn.addEventListener('click', function () { removeOverlay(overlay); });

        headerRow.appendChild(heading);
        headerRow.appendChild(closeBtn);
        box.appendChild(headerRow);

        renderReportBlocks(box, _reports, refreshDialog);

        overlay.appendChild(box);
        document.body.appendChild(overlay);

        overlay.addEventListener('click', function (e) {
            if (e.target === overlay) removeOverlay(overlay);
        });

        var dialogPollTimer = setInterval(function () {
            if (!document.body.contains(overlay)) {
                clearInterval(dialogPollTimer);
                _dialogRefresh = null;
                return;
            }
            refreshDialog();
        }, 5000);

        _dialogRefresh = refreshDialog;

        function refreshDialog() {
            var client = getClient();
            if (!client) return;
            client.getJSON(client.getUrl('/EmbyReporter/MyReports')).then(function (reports) {
                if (!document.body.contains(overlay)) return;
                _reports = reports || [];
                _count = _reports.length;
                syncBadge();
                renderReportBlocks(box, _reports, refreshDialog);
            }).catch(function () {});
        }
    }

    function removeOverlay(overlay) {
        if (overlay && overlay.parentNode) overlay.parentNode.removeChild(overlay);
    }

    function sendComment(reportId, text) {
        var client = getClient();
        if (!client) return Promise.reject('no client');

        return client.ajax({
            type: 'POST',
            url: client.getUrl('/EmbyReporter/Reports/UserComment'),
            contentType: 'application/json',
            data: JSON.stringify({ ReportId: reportId, Text: text })
        });
    }

    function sendResponse(reportId, confirmed, text) {
        var client = getClient();
        if (!client) return Promise.reject('no client');

        return client.ajax({
            type: 'POST',
            url: client.getUrl('/EmbyReporter/Reports/UserResponse'),
            contentType: 'application/json',
            data: JSON.stringify({ ReportId: reportId, Confirmed: confirmed, Text: text })
        });
    }

    window.addEventListener('hashchange', function () {
        if (_count > 0 && !document.getElementById(BADGE_ID)) {
            scheduleInject(800);
        }
    });

    setInterval(function () {
        if (_count > 0 && !document.getElementById(BADGE_ID)) {
            scheduleInject(0);
        }
    }, 3000);

    function listenForServerPush() {
        if (typeof require !== 'function') return;
        require(['events'], function (events) {
            var client = getClient();
            if (!client) return;
            events.on(client, 'message', function (e, msg) {
                if (msg && msg.MessageType === 'EmbyReporterUpdate') {
                    fetchMessages();
                    if (_dialogRefresh) _dialogRefresh();
                }
            });
        });
    }

    function start() {
        var navObserver = new MutationObserver(function () {
            if (!_navMenuItem || !document.body.contains(_navMenuItem)) {
                injectNavMenuItem();
            }
        });
        navObserver.observe(document.body, { childList: true, subtree: true });

        listenForServerPush();
        fetchMessages();
        setInterval(fetchMessages, POLL_MS);
    }

    function waitForClient() {
        if (getClient()) {
            start();
        } else {
            setTimeout(waitForClient, 500);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', waitForClient);
    } else {
        waitForClient();
    }
}());
