document.addEventListener('DOMContentLoaded', function () {
    // Элементы шагов
    const step1 = document.getElementById('step1');
    const step2 = document.getElementById('step2');
    const step3 = document.getElementById('step3');
    const step4 = document.getElementById('step4');

    // Элементы формы
    const emailInput = document.getElementById('email');
    const hiddenEmail = document.getElementById('hiddenEmail');
    const resetEmail = document.getElementById('resetEmail');
    const emailDisplay = document.getElementById('emailDisplay');
    const verificationCode = document.getElementById('verificationCode');
    const newPassword = document.getElementById('newPassword');
    const confirmPassword = document.getElementById('confirmPassword');

    // Кнопки
    const sendCodeBtn = document.getElementById('sendCodeBtn');
    const verifyCodeBtn = document.getElementById('verifyCodeBtn');
    const resetPasswordBtn = document.getElementById('resetPasswordBtn');
    const resendCodeBtn = document.getElementById('resendCodeBtn');
    const backToStep1Btn = document.getElementById('backToStep1Btn');
    const backToStep2Btn = document.getElementById('backToStep2Btn');

    // Элементы для силы пароля
    const passwordStrengthBar = document.getElementById('passwordStrengthBar');
    const passwordStrengthText = document.getElementById('passwordStrengthText');

    // Таймеры
    const timerElement = document.getElementById('timer');
    const resendTimerElement = document.getElementById('resendTimer');

    // Данные
    let currentEmail = '';
    let codeExpiryTime = null;
    let resendTimer = 60;

    // Функции переключения видимости пароля
    function togglePasswordVisibility(input, button) {
        const type = input.getAttribute('type') === 'password' ? 'text' : 'password';
        input.setAttribute('type', type);
        button.innerHTML = type === 'password' ? '<i class="fas fa-eye"></i>' : '<i class="fas fa-eye-slash"></i>';
    }

    document.getElementById('toggleNewPassword')?.addEventListener('click', function () {
        togglePasswordVisibility(newPassword, this);
    });

    document.getElementById('toggleConfirmPassword')?.addEventListener('click', function () {
        togglePasswordVisibility(confirmPassword, this);
    });

    // Проверка силы пароля
    function checkPasswordStrength(password) {
        let strength = 0;
        if (password.length >= 6) strength++;
        if (password.length >= 8) strength++;
        if (/[A-Z]/.test(password)) strength++;
        if (/[a-z]/.test(password)) strength++;
        if (/[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password)) strength++;
        return strength;
    }

    function updatePasswordStrength() {
        const password = newPassword.value;
        const strength = checkPasswordStrength(password);
        const percentage = Math.min((strength / 5) * 100, 100);

        passwordStrengthBar.style.width = percentage + '%';

        let color = '#dc3545';
        let text = 'Очень слабый';

        if (strength >= 2) {
            color = '#ffc107';
            text = 'Слабый';
        }
        if (strength >= 3) {
            color = '#fd7e14';
            text = 'Средний';
        }
        if (strength >= 4) {
            color = '#20c997';
            text = 'Хороший';
        }
        if (strength >= 5) {
            color = '#198754';
            text = 'Отличный';
        }

        passwordStrengthBar.style.backgroundColor = color;
        passwordStrengthText.textContent = 'Сила пароля: ' + text;

        const reqLength = document.getElementById('reqLength');
        const reqUppercase = document.getElementById('reqUppercase');
        const reqLowercase = document.getElementById('reqLowercase');
        const reqSpecial = document.getElementById('reqSpecial');

        if (reqLength) {
            if (password.length >= 6) {
                reqLength.classList.add('valid');
                reqLength.classList.remove('text-danger');
                reqLength.querySelector('i').className = 'fas fa-check me-1';
            } else {
                reqLength.classList.remove('valid');
                reqLength.classList.add('text-danger');
                reqLength.querySelector('i').className = 'fas fa-times me-1';
            }
        }

        if (reqUppercase) {
            if (/[A-Z]/.test(password)) {
                reqUppercase.classList.add('valid');
                reqUppercase.classList.remove('text-danger');
                reqUppercase.querySelector('i').className = 'fas fa-check me-1';
            } else {
                reqUppercase.classList.remove('valid');
                reqUppercase.classList.add('text-danger');
                reqUppercase.querySelector('i').className = 'fas fa-times me-1';
            }
        }

        if (reqLowercase) {
            if (/[a-z]/.test(password)) {
                reqLowercase.classList.add('valid');
                reqLowercase.classList.remove('text-danger');
                reqLowercase.querySelector('i').className = 'fas fa-check me-1';
            } else {
                reqLowercase.classList.remove('valid');
                reqLowercase.classList.add('text-danger');
                reqLowercase.querySelector('i').className = 'fas fa-times me-1';
            }
        }

        if (reqSpecial) {
            const hasSpecial = /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password);
            if (hasSpecial) {
                reqSpecial.classList.add('valid');
                reqSpecial.classList.remove('text-danger');
                reqSpecial.querySelector('i').className = 'fas fa-check me-1';
            } else {
                reqSpecial.classList.remove('valid');
                reqSpecial.classList.add('text-danger');
                reqSpecial.querySelector('i').className = 'fas fa-times me-1';
            }
        }
    }

    function checkPasswordMatch() {
        const password = newPassword.value;
        const confirm = confirmPassword.value;
        const matchElement = document.getElementById('passwordMatch');
        const successElement = document.getElementById('passwordSuccess');

        if (confirm === '') {
            if (matchElement) matchElement.classList.add('d-none');
            if (successElement) successElement.classList.add('d-none');
            return false;
        }

        if (password === confirm) {
            if (matchElement) matchElement.classList.add('d-none');
            if (successElement) successElement.classList.remove('d-none');
            return true;
        } else {
            if (matchElement) matchElement.classList.remove('d-none');
            if (successElement) successElement.classList.add('d-none');
            return false;
        }
    }

    function validateEmail(email) {
        const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return re.test(email);
    }

    function validateStep3() {
        const password = newPassword.value;
        const passwordValid = password.trim() !== '' &&
            password.length >= 6 &&
            /[A-Z]/.test(password) &&
            /[a-z]/.test(password) &&
            /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password);
        const confirmValid = checkPasswordMatch();
        return passwordValid && confirmValid;
    }

    function goToStep(stepNumber) {
        step1.classList.add('d-none');
        step2.classList.add('d-none');
        step3.classList.add('d-none');
        step4.classList.add('d-none');

        if (stepNumber === 1) {
            step1.classList.remove('d-none');
        } else if (stepNumber === 2) {
            step2.classList.remove('d-none');
            startTimers();
        } else if (stepNumber === 3) {
            step3.classList.remove('d-none');
            updatePasswordStrength();
        } else if (stepNumber === 4) {
            step4.classList.remove('d-none');
        }
    }

    function startTimers() {
        let codeTime = 15 * 60;
        codeExpiryTime = new Date(Date.now() + codeTime * 1000);

        const codeTimer = setInterval(function () {
            const now = new Date();
            const diff = Math.floor((codeExpiryTime - now) / 1000);

            if (diff <= 0) {
                clearInterval(codeTimer);
                timerElement.textContent = '00:00';
                alert('Время действия кода истекло. Пожалуйста, запросите новый код.');
                goToStep(1);
                return;
            }

            const minutes = Math.floor(diff / 60);
            const seconds = diff % 60;
            timerElement.textContent = minutes.toString().padStart(2, '0') + ':' + seconds.toString().padStart(2, '0');
        }, 1000);

        resendTimer = 60;
        resendCodeBtn.disabled = true;

        const resendInterval = setInterval(function () {
            resendTimer--;
            resendTimerElement.textContent = resendTimer;

            if (resendTimer <= 0) {
                clearInterval(resendInterval);
                resendCodeBtn.disabled = false;
                resendCodeBtn.innerHTML = 'Отправить код повторно';
            } else {
                resendCodeBtn.innerHTML = 'Отправить код повторно (<span id="resendTimer">' + resendTimer + '</span>)';
            }
        }, 1000);
    }

    // Отправка кода на email
    document.getElementById('emailForm').addEventListener('submit', async function (e) {
        e.preventDefault();

        const email = emailInput.value.trim();

        if (!email) {
            showError('emailError', 'Введите email адрес');
            return;
        }

        if (!validateEmail(email)) {
            showError('emailError', 'Введите корректный email адрес');
            return;
        }

        sendCodeBtn.disabled = true;
        sendCodeBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Отправка...';

        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

            const response = await fetch('/Account/SendResetCode', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify({ email: email })
            });

            const result = await response.json();

            if (result.success) {
                currentEmail = email;
                hiddenEmail.value = email;
                resetEmail.value = email;
                emailDisplay.textContent = email;
                hideError('emailError');
                goToStep(2);
            } else {
                showError('emailError', result.message || 'Ошибка при отправке кода');
            }
        } catch (error) {
            console.error('Ошибка:', error);
            showError('emailError', 'Произошла ошибка при отправке кода');
        } finally {
            sendCodeBtn.disabled = false;
            sendCodeBtn.innerHTML = '<i class="fas fa-paper-plane me-2"></i>Отправить код';
        }
    });

    // Проверка кода
    document.getElementById('codeForm').addEventListener('submit', async function (e) {
        e.preventDefault();

        const code = verificationCode.value.trim();

        if (!code || code.length !== 6) {
            showError('codeError', 'Введите 6-значный код');
            return;
        }

        verifyCodeBtn.disabled = true;
        verifyCodeBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Проверка...';

        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

            const response = await fetch('/Account/VerifyResetCode', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify({
                    email: currentEmail,
                    code: code
                })
            });

            const result = await response.json();

            if (result.success) {
                hideError('codeError');
                goToStep(3);
            } else {
                showError('codeError', result.message || 'Неверный код');
            }
        } catch (error) {
            console.error('Ошибка:', error);
            showError('codeError', 'Произошла ошибка при проверке кода');
        } finally {
            verifyCodeBtn.disabled = false;
            verifyCodeBtn.innerHTML = '<i class="fas fa-check me-2"></i>Подтвердить код';
        }
    });

    // Сброс пароля
    document.getElementById('newPasswordForm').addEventListener('submit', async function (e) {
        e.preventDefault();

        if (!validateStep3()) {
            alert('Пожалуйста, проверьте правильность ввода нового пароля');
            return;
        }

        resetPasswordBtn.disabled = true;
        resetPasswordBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Сохранение...';

        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

            const response = await fetch('/Account/ResetPassword', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify({
                    email: currentEmail,
                    newPassword: newPassword.value
                })
            });

            const result = await response.json();

            if (result.success) {
                goToStep(4);
            } else {
                alert(result.message || 'Ошибка при смене пароля');
            }
        } catch (error) {
            console.error('Ошибка:', error);
            alert('Произошла ошибка при смене пароля');
        } finally {
            resetPasswordBtn.disabled = false;
            resetPasswordBtn.innerHTML = '<i class="fas fa-save me-2"></i>Сохранить новый пароль';
        }
    });

    // Повторная отправка кода
    resendCodeBtn.addEventListener('click', async function () {
        if (this.disabled) return;

        try {
            this.disabled = true;
            this.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Отправка...';

            const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

            const response = await fetch('/Account/SendResetCode', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify({ email: currentEmail })
            });

            const result = await response.json();

            if (result.success) {
                // Перезапускаем таймеры
                startTimers();
            } else {
                alert(result.message || 'Ошибка при отправке кода');
            }
        } catch (error) {
            console.error('Ошибка:', error);
            alert('Произошла ошибка при отправке кода');
        }
    });

    // Обработчики для кнопок "Назад"
    backToStep1Btn.addEventListener('click', function () {
        goToStep(1);
    });

    backToStep2Btn.addEventListener('click', function () {
        goToStep(2);
    });

    // Ввод кода
    verificationCode.addEventListener('input', function () {
        const code = this.value.replace(/\D/g, '');
        this.value = code;

        if (code.length === 6) {
            hideError('codeError');
            verifyCodeBtn.disabled = false;
        } else {
            verifyCodeBtn.disabled = true;
        }
    });

    // Проверка пароля в реальном времени
    newPassword.addEventListener('input', function () {
        updatePasswordStrength();
        resetPasswordBtn.disabled = !validateStep3();
    });

    confirmPassword.addEventListener('input', function () {
        checkPasswordMatch();
        resetPasswordBtn.disabled = !validateStep3();
    });

    // Вспомогательные функции для ошибок
    function showError(elementId, message) {
        const errorElement = document.getElementById(elementId);
        const messageSpan = document.getElementById(elementId + 'Message');
        if (messageSpan) messageSpan.textContent = message;
        errorElement.classList.remove('d-none');
    }

    function hideError(elementId) {
        document.getElementById(elementId).classList.add('d-none');
    }
});