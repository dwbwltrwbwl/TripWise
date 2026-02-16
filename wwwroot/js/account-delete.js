// account-delete.js
(function () {
    'use strict';

    // Функция для отладки
    function debugLog(message, data = null) {
        const timestamp = new Date().toISOString();
        console.log(`[DeleteAccount][${timestamp}] ${message}`);
        if (data) {
            console.log('Data:', data);
        }
    }

    debugLog('Script started loading');

    if (document.readyState === 'loading') {
        debugLog('DOM still loading, adding event listener');
        document.addEventListener('DOMContentLoaded', () => {
            debugLog('DOMContentLoaded fired');
            initDeleteAccount();
        });
    } else {
        debugLog('DOM already loaded, initializing immediately');
        initDeleteAccount();
    }

    function initDeleteAccount() {
        debugLog('Initializing delete account functionality');

        // Проверяем наличие элементов на странице
        const step1 = document.getElementById('step1');
        const step2 = document.getElementById('step2');
        const step3 = document.getElementById('step3');

        debugLog('Step elements found:', {
            step1: !!step1,
            step2: !!step2,
            step3: !!step3
        });

        // Если нет элементов шагов, значит мы не на странице удаления
        if (!step1 || !step2 || !step3) {
            debugLog('Not on delete account page, exiting');
            return;
        }

        // Получаем все элементы формы
        const elements = {
            confirmCheckbox: document.getElementById('confirmCheckbox'),
            sendCodeBtn: document.getElementById('sendCodeBtn'),
            verificationCode: document.getElementById('verificationCode'),
            confirmDeleteBtn: document.getElementById('confirmDeleteBtn'),
            resendCodeBtn: document.getElementById('resendCodeBtn'),
            backToStep1Btn: document.getElementById('backToStep1Btn'),
            timerElement: document.getElementById('timer'),
            resendTimerElement: document.getElementById('resendTimer'),
            messageContainer: document.getElementById('messageContainer'),
            codeError: document.getElementById('codeError'),
            deleteForm: document.getElementById('deleteForm')
        };

        debugLog('Form elements found:', {
            confirmCheckbox: !!elements.confirmCheckbox,
            sendCodeBtn: !!elements.sendCodeBtn,
            verificationCode: !!elements.verificationCode,
            confirmDeleteBtn: !!elements.confirmDeleteBtn,
            resendCodeBtn: !!elements.resendCodeBtn,
            backToStep1Btn: !!elements.backToStep1Btn,
            timerElement: !!elements.timerElement,
            resendTimerElement: !!elements.resendTimerElement,
            messageContainer: !!elements.messageContainer,
            codeError: !!elements.codeError,
            deleteForm: !!elements.deleteForm
        });

        // Данные
        let codeExpiryTime = null;
        let resendTimerSeconds = 60;
        let codeTimerInterval = null;
        let resendTimerInterval = null;
        let isProcessing = false;

        // Получаем CSRF токен
        function getCsrfToken() {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            debugLog('CSRF token found:', !!token);
            if (!token) {
                debugLog('WARNING: CSRF token not found!');
            }
            return token;
        }

        // Функция для безопасного fetch с отладкой
        async function safeFetch(url, options) {
            debugLog(`Fetching: ${url}`, options);

            try {
                const response = await fetch(url, options);
                debugLog(`Response status: ${response.status} ${response.statusText}`);

                // Проверяем content-type
                const contentType = response.headers.get('content-type');
                debugLog('Response content-type:', contentType);

                if (!response.ok) {
                    const errorText = await response.text();
                    debugLog('Error response body:', errorText.substring(0, 200));
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }

                // Пытаемся распарсить JSON
                try {
                    const data = await response.json();
                    debugLog('Response data:', data);
                    return { success: true, data };
                } catch (jsonError) {
                    const text = await response.text();
                    debugLog('Failed to parse JSON, got text:', text.substring(0, 200));
                    throw new Error('Invalid JSON response from server');
                }
            } catch (error) {
                debugLog('Fetch error:', error);
                return {
                    success: false,
                    error: error.message,
                    type: error.name
                };
            }
        }

        // Включение кнопки при чекбоксе
        if (elements.confirmCheckbox) {
            debugLog('Adding checkbox change listener');
            elements.confirmCheckbox.addEventListener('change', function () {
                const isChecked = this.checked;
                debugLog('Checkbox changed:', isChecked);
                elements.sendCodeBtn.disabled = !isChecked;
            });
        }

        // Ограничение ввода только цифрами
        if (elements.verificationCode) {
            debugLog('Adding verification code input listener');
            elements.verificationCode.addEventListener('input', function (e) {
                const oldValue = this.value;
                this.value = this.value.replace(/[^0-9]/g, '');

                if (oldValue !== this.value) {
                    debugLog('Code input filtered:', { before: oldValue, after: this.value });
                }

                if (this.value.length > 6) {
                    this.value = this.value.slice(0, 6);
                    debugLog('Code trimmed to 6 digits');
                }

                if (this.value.length === 6) {
                    debugLog('Full 6-digit code entered');
                    hideError('codeError');
                    if (elements.confirmDeleteBtn) {
                        elements.confirmDeleteBtn.disabled = false;
                    }
                } else {
                    if (elements.confirmDeleteBtn) {
                        elements.confirmDeleteBtn.disabled = true;
                    }
                }
            });

            // Добавляем обработчик потери фокуса для отладки
            elements.verificationCode.addEventListener('blur', function () {
                debugLog('Code input lost focus, value:', this.value);
            });
        }

        // Функция перехода между шагами
        function goToStep(stepNumber) {
            debugLog(`Going to step ${stepNumber}`);

            // Очищаем таймеры при смене шага
            cleanupTimers();

            // Скрываем все шаги
            step1.classList.add('d-none');
            step2.classList.add('d-none');
            step3.classList.add('d-none');

            // Показываем нужный шаг
            if (stepNumber === 1) {
                debugLog('Showing step 1');
                step1.classList.remove('d-none');
                if (elements.confirmCheckbox) {
                    elements.confirmCheckbox.checked = false;
                    elements.sendCodeBtn.disabled = true;
                }
                if (elements.messageContainer) {
                    elements.messageContainer.innerHTML = '';
                }
            } else if (stepNumber === 2) {
                debugLog('Showing step 2');
                step2.classList.remove('d-none');
                startTimers();
                if (elements.verificationCode) {
                    elements.verificationCode.value = '';
                    elements.verificationCode.focus();
                }
                if (elements.confirmDeleteBtn) {
                    elements.confirmDeleteBtn.disabled = true;
                }
            } else if (stepNumber === 3) {
                debugLog('Showing step 3');
                step3.classList.remove('d-none');
            }
        }

        // Очистка таймеров
        function cleanupTimers() {
            debugLog('Cleaning up timers');
            if (codeTimerInterval) {
                clearInterval(codeTimerInterval);
                codeTimerInterval = null;
                debugLog('Code timer cleared');
            }
            if (resendTimerInterval) {
                clearInterval(resendTimerInterval);
                resendTimerInterval = null;
                debugLog('Resend timer cleared');
            }
        }

        // Запуск таймеров
        function startTimers() {
            debugLog('Starting timers');
            cleanupTimers();

            const codeTime = 15 * 60; // 15 минут в секундах
            codeExpiryTime = new Date(Date.now() + codeTime * 1000);
            debugLog('Code expiry time:', codeExpiryTime);

            // Таймер для кода
            codeTimerInterval = setInterval(updateCodeTimer, 1000);

            // Таймер для повторной отправки
            resendTimerSeconds = 60;
            updateResendButton();
            resendTimerInterval = setInterval(updateResendTimer, 1000);

            debugLog('Timers started');
        }

        // Обновление таймера кода
        function updateCodeTimer() {
            const now = new Date();
            const diff = Math.floor((codeExpiryTime - now) / 1000);

            if (diff <= 0) {
                debugLog('Code expired');
                clearInterval(codeTimerInterval);
                if (elements.timerElement) elements.timerElement.textContent = '00:00';
                showMessage('warning', 'Время действия кода истекло. Запросите новый код.');
                goToStep(1);
                return;
            }

            const minutes = Math.floor(diff / 60);
            const seconds = diff % 60;
            if (elements.timerElement) {
                elements.timerElement.textContent = `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
            }
        }

        // Обновление таймера повторной отправки
        function updateResendTimer() {
            resendTimerSeconds--;

            if (resendTimerSeconds <= 0) {
                clearInterval(resendTimerInterval);
                if (elements.resendCodeBtn) {
                    elements.resendCodeBtn.disabled = false;
                    elements.resendCodeBtn.innerHTML = 'Отправить код повторно';
                    debugLog('Resend button enabled');
                }
            } else {
                updateResendButton();
            }

            if (elements.resendTimerElement) {
                elements.resendTimerElement.textContent = resendTimerSeconds;
            }
        }

        // Обновление текста кнопки повторной отправки
        function updateResendButton() {
            if (elements.resendCodeBtn) {
                elements.resendCodeBtn.disabled = true;
                elements.resendCodeBtn.innerHTML = `Отправить код повторно (${resendTimerSeconds}с)`;
            }
        }

        // Отправка кода
        if (elements.sendCodeBtn) {
            debugLog('Adding send code button listener');
            elements.sendCodeBtn.addEventListener('click', async function (e) {
                e.preventDefault();
                debugLog('Send code button clicked');

                if (isProcessing) {
                    debugLog('Already processing, ignoring click');
                    return;
                }

                isProcessing = true;
                const btn = this;
                const originalText = btn.innerHTML;

                btn.disabled = true;
                btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Отправка...';

                try {
                    const token = getCsrfToken();
                    if (!token) {
                        throw new Error('CSRF token not found');
                    }

                    debugLog('Sending code request');
                    const result = await safeFetch('/Account/SendDeleteCode', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'RequestVerificationToken': token
                        },
                        body: JSON.stringify({})
                    });

                    if (!result.success) {
                        throw new Error(result.error || 'Network error');
                    }

                    if (result.data.success) {
                        debugLog('Code sent successfully');
                        showMessage('success', result.data.message);
                        goToStep(2);
                    } else {
                        debugLog('Server returned error:', result.data);
                        showMessage('danger', result.data.message || 'Ошибка при отправке кода');
                        btn.disabled = false;
                        btn.innerHTML = originalText;
                    }
                } catch (error) {
                    debugLog('Error in send code:', error);
                    showMessage('danger', 'Ошибка: ' + error.message);
                    btn.disabled = false;
                    btn.innerHTML = originalText;
                } finally {
                    isProcessing = false;
                    debugLog('Send code processing finished');
                }
            });
        }

        // Повторная отправка кода
        if (elements.resendCodeBtn) {
            debugLog('Adding resend code button listener');
            elements.resendCodeBtn.addEventListener('click', async function (e) {
                e.preventDefault();
                debugLog('Resend code button clicked');

                if (isProcessing || this.disabled) {
                    debugLog('Button disabled or processing, ignoring');
                    return;
                }

                isProcessing = true;
                const btn = this;
                const originalText = btn.innerHTML;

                btn.disabled = true;
                btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Отправка...';

                try {
                    const token = getCsrfToken();
                    if (!token) {
                        throw new Error('CSRF token not found');
                    }

                    debugLog('Sending resend code request');
                    const result = await safeFetch('/Account/SendDeleteCode', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'RequestVerificationToken': token
                        },
                        body: JSON.stringify({})
                    });

                    if (!result.success) {
                        throw new Error(result.error || 'Network error');
                    }

                    if (result.data.success) {
                        debugLog('Code resent successfully');
                        showMessage('success', 'Новый код отправлен');
                        startTimers();
                    } else {
                        debugLog('Server returned error on resend:', result.data);
                        showMessage('danger', result.data.message || 'Ошибка при отправке кода');
                        btn.disabled = false;
                        btn.innerHTML = 'Отправить код повторно';
                    }
                } catch (error) {
                    debugLog('Error in resend code:', error);
                    showMessage('danger', 'Ошибка: ' + error.message);
                    btn.disabled = false;
                    btn.innerHTML = 'Отправить код повторно';
                } finally {
                    isProcessing = false;
                    debugLog('Resend code processing finished');
                }
            });
        }

        // Подтверждение удаления
        if (elements.confirmDeleteBtn) {
            debugLog('Adding confirm delete button listener');

            // Используем клик по кнопке вместо submit формы
            elements.confirmDeleteBtn.addEventListener('click', async function (e) {
                e.preventDefault();
                debugLog('Confirm delete button clicked');

                if (isProcessing) {
                    debugLog('Already processing, ignoring click');
                    return;
                }

                const code = elements.verificationCode?.value.trim();
                debugLog('Entered code:', code);

                if (!code || code.length !== 6) {
                    debugLog('Invalid code length');
                    showError('codeError', 'Введите 6-значный код подтверждения');
                    return;
                }

                if (!confirm('Вы уверены, что хотите окончательно удалить аккаунт? Это действие невозможно отменить.')) {
                    debugLog('User cancelled deletion');
                    return;
                }

                isProcessing = true;
                const btn = this;
                const originalText = btn.innerHTML;

                btn.disabled = true;
                btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Удаление...';

                try {
                    const token = getCsrfToken();
                    if (!token) {
                        throw new Error('CSRF token not found');
                    }

                    debugLog('Sending delete confirmation request');
                    const result = await safeFetch('/Account/ConfirmDelete', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'RequestVerificationToken': token
                        },
                        body: JSON.stringify({ code: code })
                    });

                    if (!result.success) {
                        throw new Error(result.error || 'Network error');
                    }

                    debugLog('Delete confirmation response:', result.data);

                    if (result.data.success) {
                        debugLog('Account deleted successfully');
                        showMessage('success', result.data.message);
                        goToStep(3);

                        cleanupTimers();

                        setTimeout(() => {
                            debugLog('Redirecting to home page');
                            window.location.href = result.data.redirectUrl || '/';
                        }, 3000);
                    } else {
                        debugLog('Server returned error on delete:', result.data);
                        showError('codeError', result.data.message || 'Неверный код подтверждения');
                        btn.disabled = false;
                        btn.innerHTML = originalText;

                        if (elements.verificationCode) {
                            elements.verificationCode.value = '';
                            elements.verificationCode.focus();
                        }
                    }
                } catch (error) {
                    debugLog('Error in confirm delete:', error);
                    showError('codeError', 'Ошибка: ' + error.message);
                    btn.disabled = false;
                    btn.innerHTML = originalText;
                } finally {
                    isProcessing = false;
                    debugLog('Confirm delete processing finished');
                }
            });
        }

        // Кнопка "Назад"
        if (elements.backToStep1Btn) {
            debugLog('Adding back button listener');
            elements.backToStep1Btn.addEventListener('click', function (e) {
                e.preventDefault();
                debugLog('Back button clicked');
                goToStep(1);
            });
        }

        // Обработка формы для предотвращения стандартной отправки
        if (elements.deleteForm) {
            debugLog('Adding form submit prevention');
            elements.deleteForm.addEventListener('submit', function (e) {
                e.preventDefault();
                debugLog('Form submit prevented');
                return false;
            });
        }

        // Отображение ошибки
        function showError(elementId, message) {
            debugLog(`Showing error: ${message}`, { elementId });
            const errorElement = document.getElementById(elementId);
            const messageSpan = document.getElementById(elementId + 'Message');

            if (messageSpan) {
                messageSpan.textContent = message;
            }

            if (errorElement) {
                errorElement.classList.remove('d-none');

                setTimeout(() => {
                    errorElement.classList.add('d-none');
                    debugLog(`Error ${elementId} hidden`);
                }, 5000);
            }
        }

        // Скрытие ошибки
        function hideError(elementId) {
            const errorElement = document.getElementById(elementId);
            if (errorElement) {
                errorElement.classList.add('d-none');
            }
        }

        // Отображение сообщения
        function showMessage(type, text) {
            debugLog(`Showing message: ${type} - ${text}`);

            if (!elements.messageContainer) {
                debugLog('Message container not found');
                return;
            }

            const alertClass = type === 'success' ? 'alert-success' :
                type === 'warning' ? 'alert-warning' : 'alert-danger';

            const icon = type === 'success' ? 'fa-check-circle' :
                type === 'warning' ? 'fa-exclamation-triangle' : 'fa-exclamation-circle';

            const alertId = 'alert-' + Date.now();

            const html = `
                <div id="${alertId}" class="alert ${alertClass} alert-dismissible fade show" role="alert">
                    <i class="fas ${icon} me-2"></i>${text}
                    <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
                </div>`;

            elements.messageContainer.innerHTML = html;
            debugLog('Message displayed', { alertId, type, text });

            setTimeout(() => {
                const alert = document.getElementById(alertId);
                if (alert) {
                    alert.classList.remove('show');
                    setTimeout(() => {
                        if (elements.messageContainer.innerHTML.includes(alertId)) {
                            elements.messageContainer.innerHTML = '';
                            debugLog('Message removed');
                        }
                    }, 300);
                }
            }, 5000);
        }

        // Очистка при уходе со страницы
        window.addEventListener('beforeunload', function () {
            debugLog('Page unloading, cleaning up');
            cleanupTimers();
        });

        debugLog('Initialization complete');
    }
})();