
    $(document).ready(function() {
        console.log('Document ready, newsletter script loaded');

    // Проверяем наличие формы
    if ($('#newsletterForm').length === 0) {
        console.error('Newsletter form not found!');
    return;
        }

    console.log('Newsletter form found, attaching submit handler');

    // Обработка подписки на рассылку
    $('#newsletterForm').on('submit', function(e) {
        e.preventDefault();
    console.log('Form submitted');

    var email = $('#newsletterEmail').val().trim();
    console.log('Email entered:', email);

    // Базовая валидация
    if (!email) {
        alert('Введите email адрес');
    return;
            }

    // Простая валидация email
    if (!email.includes('@') || !email.includes('.')) {
        alert('Введите корректный email адрес');
    return;
            }

    // Получаем CSRF токен
    var token = $('input[name="__RequestVerificationToken"]').val();
    console.log('CSRF token exists:', !!token);

    // Показываем загрузку
    var btn = $('#subscribeBtn');
    btn.html('<span class="spinner-border spinner-border-sm"></span>');
    btn.prop('disabled', true);

    // Отправляем тестовый запрос
    $.ajax({
        url: '/Newsletter/Subscribe',
    type: 'POST',
    data: {
        __RequestVerificationToken: token,
    email: email
                },
    success: function(response, status, xhr) {
        console.log('AJAX success:', response);
    console.log('Status:', status);
    console.log('Response headers:', xhr.getAllResponseHeaders());

    if (response && response.success) {
        alert(response.message);
    $('#newsletterEmail').val('');
                    } else if (response) {
        alert(response.message || 'Неизвестная ошибка');
                    } else {
        alert('Пустой ответ от сервера');
                    }
                },
    error: function(xhr, status, error) {
        console.error('AJAX error:', {
            status: xhr.status,
            statusText: xhr.statusText,
            responseText: xhr.responseText,
            error: error
        });

    var errorMsg = 'Ошибка: ' + xhr.status + ' ' + xhr.statusText;
    if (xhr.responseText) {
                        try {
                            var json = JSON.parse(xhr.responseText);
    errorMsg = json.message || errorMsg;
                        } catch(e) {
        errorMsg += '\n' + xhr.responseText.substring(0, 100);
                        }
                    }

    alert(errorMsg);
                },
    complete: function() {
        btn.html('Подписаться');
    btn.prop('disabled', false);
                }
            });
        });

    console.log('Newsletter form handler attached successfully');
    });