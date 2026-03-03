// Глобальные переменные
let currentDropdown = null;
let currentInput = null;
let timeoutId;
let isUserAuthenticated = false;
let userId = null;
let isCheckingFavorites = false; // Добавить эту строку
let favoriteCheckQueue = []; // Очередь для проверки

// ==================== ФУНКЦИИ АВТОРИЗАЦИИ ====================
async function checkAuthStatus() {
    try {
        console.log('Проверяем статус авторизации...');
        const response = await fetch('/api/auth/status', {
            method: 'GET',
            credentials: 'include',
            headers: { 'Accept': 'application/json' }
        });

        console.log('Статус ответа:', response.status);

        if (response.ok) {
            const data = await response.json();
            console.log('Данные авторизации:', data);
            isUserAuthenticated = data.isAuthenticated;
            userId = data.userId;
            console.log('Статус авторизации:', isUserAuthenticated, 'User ID:', userId);
        } else {
            console.error('Ошибка при проверке авторизации:', response.status);
            isUserAuthenticated = false;
            userId = null;
        }
    } catch (error) {
        console.error('Ошибка проверки авторизации:', error);
        isUserAuthenticated = false;
        userId = null;
    }
}

// ==================== АВТОЗАПОЛНЕНИЕ ГОРОДОВ ====================
async function searchCitiesFromTravelPayouts(query, dropdown) {
    if (query.length < 2) {
        dropdown.style.display = 'none';
        return;
    }

    try {
        const endpoint = `https://autocomplete.travelpayouts.com/places2?term=${encodeURIComponent(query)}&locale=ru&types[]=airport&types[]=city`;
        console.log('Searching cities with query:', query);

        const response = await fetch(endpoint, {
            method: 'GET',
            headers: {
                'Accept': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            },
        });

        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);

        const data = await response.json();
        console.log('Received cities data:', data);

        let cities = [];
        if (Array.isArray(data)) {
            cities = data.map(item => {
                if (item.code && item.name) {
                    return {
                        name: item.name,
                        country: item.country_name || item.country_code || '',
                        airport: item.type === 'airport' ? item.name : '',
                        code: item.code,
                        type: item.type || 'city'
                    };
                }
                return null;
            }).filter(item => item !== null && item.name && item.code);
        }

        console.log('Processed cities:', cities);
        showAutocompleteResults(cities, dropdown, query);
    } catch (error) {
        console.error('Ошибка при поиске городов:', error);
        const mockCities = getMockCities(query);
        showAutocompleteResults(mockCities, dropdown, query);
    }
}

function getMockCities(query) {
    const allCities = [
        { code: "MOW", name: "Москва", country: "Россия", type: "city", airport: "" },
        { code: "LED", name: "Санкт-Петербург", country: "Россия", type: "city", airport: "" },
        { code: "AER", name: "Сочи", country: "Россия", type: "city", airport: "" },
        { code: "KZN", name: "Казань", country: "Россия", type: "city", airport: "" },
        { code: "SVX", name: "Екатеринбург", country: "Россия", type: "city", airport: "" },
        { code: "OVB", name: "Новосибирск", country: "Россия", type: "city", airport: "" },
        { code: "KRR", name: "Краснодар", country: "Россия", type: "city", airport: "" },
        { code: "SIP", name: "Симферополь", country: "Россия", type: "city", airport: "" },
        { code: "MRV", name: "Минеральные Воды", country: "Россия", type: "city", airport: "" },
        { code: "KGD", name: "Калининград", country: "Россия", type: "city", airport: "" },
        { code: "TJM", name: "Тюмень", country: "Россия", type: "city", airport: "" },
        { code: "SVO", name: "Шереметьево", country: "Россия", type: "airport", airport: "Шереметьево" },
        { code: "DME", name: "Домодедово", country: "Россия", type: "airport", airport: "Домодедово" },
        { code: "VKO", name: "Внуково", country: "Россия", type: "airport", airport: "Внуково" }
    ];

    return allCities.filter(city =>
        city.name.toLowerCase().includes(query.toLowerCase()) ||
        city.code.toLowerCase().includes(query.toLowerCase())
    );
}

function showAutocompleteResults(cities, dropdown, query) {
    dropdown.innerHTML = '';
    if (!cities || cities.length === 0) {
        const noResults = document.createElement('div');
        noResults.className = 'autocomplete-item';
        noResults.textContent = 'Ничего не найдено';
        dropdown.appendChild(noResults);
        dropdown.style.display = 'block';
        return;
    }

    const limitedCities = cities.slice(0, 8);
    limitedCities.forEach(city => {
        const item = document.createElement('div');
        item.className = 'autocomplete-item';

        let displayText = '';
        if (city.type === 'airport') {
            displayText = `
                <div class="city-name">${city.name}
                    <span class="city-country">${city.country}</span>
                </div>
                <div class="city-airport">Аэропорт (${city.code})</div>
            `;
        } else {
            displayText = `
                <div class="city-name">${city.name}
                    <span class="city-country">${city.country}</span>
                </div>
                <div class="city-airport">Город (${city.code})</div>
            `;
        }

        item.innerHTML = displayText;
        item.addEventListener('click', () => {
            let displayValue = `${city.name} (${city.code})`;
            currentInput.value = displayValue;
            dropdown.style.display = 'none';
        });

        dropdown.appendChild(item);
    });

    dropdown.style.display = 'block';
}

// ==================== ФУНКЦИИ ИЗБРАННОГО ====================
// Добавьте эту функцию для сброса состояния кнопок
function resetFavoriteButton(flightId) {
    const buttons = document.querySelectorAll(`[data-flight-id="${CSS.escape(flightId)}"]`);
    buttons.forEach(button => {
        const icon = button.querySelector('i');
        if (icon) {
            icon.className = 'far fa-heart fa-lg text-muted';
            button.title = 'Добавить в избранное';
            button.classList.remove('favorited');
        }
    });
}

async function toggleFavorite(flightData) {
    if (!isUserAuthenticated) {
        showAuthRequiredModal();
        return;
    }

    // Блокируем кнопку на время операции
    const button = document.querySelector(`[data-flight-id="${CSS.escape(flightData.flightId)}"]`);
    if (button) {
        button.style.pointerEvents = 'none';
        button.style.opacity = '0.6';
    }

    try {
        const flightId = flightData.flightId;
        console.log('========== ПЕРЕКЛЮЧЕНИЕ ИЗБРАННОГО ==========');
        console.log('Flight ID:', flightId);
        console.log('Flight Data:', flightData);

        // Сначала проверяем текущий статус на сервере
        console.log('1. Проверяем статус рейса на сервере...');
        const encodedFlightId = encodeURIComponent(flightId);
        const checkUrl = `/api/favorites/flights/check/${encodedFlightId}?t=${Date.now()}`;
        console.log('Check URL:', checkUrl);

        const checkResponse = await fetch(checkUrl, {
            credentials: 'include',
            headers: {
                'Accept': 'application/json',
                'Cache-Control': 'no-cache'
            }
        });

        if (!checkResponse.ok) {
            let errorMessage = `Ошибка проверки: ${checkResponse.status}`;
            try {
                const errorData = await checkResponse.json();
                errorMessage = errorData.message || errorMessage;
            } catch (e) {
                // Если не удалось распарсить JSON
            }
            throw new Error(errorMessage);
        }

        const checkData = await checkResponse.json();
        console.log('2. Результат проверки статуса:', checkData);

        let response;
        if (checkData.isFavorite) {
            // Удаляем из избранного
            console.log('3. Рейс В ИЗБРАННОМ, выполняем УДАЛЕНИЕ');
            const deleteUrl = `/api/favorites/flights/${encodedFlightId}`;
            console.log('Delete URL:', deleteUrl);

            response = await fetch(deleteUrl, {
                method: 'DELETE',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                }
            });
        } else {
            // Добавляем в избранное
            console.log('3. Рейс НЕ В ИЗБРАННОМ, выполняем ДОБАВЛЕНИЕ');

            // Убеждаемся, что все необходимые поля есть
            const favoriteData = {
                flightId: flightData.flightId,
                airline: flightData.airline || 'Авиакомпания',
                airlineCode: flightData.airlineCode || '',
                flightNumber: flightData.flightNumber || '',
                departureCity: flightData.departureCity || '',
                arrivalCity: flightData.arrivalCity || '',
                departureAirport: flightData.departureAirport || '',
                arrivalAirport: flightData.arrivalAirport || '',
                departureTime: flightData.departureTime || new Date().toISOString(),
                arrivalTime: flightData.arrivalTime || new Date().toISOString(),
                price: flightData.price || 0,
                currency: flightData.currency || 'RUB',
                transfers: flightData.transfers || 0,
                duration: flightData.duration || 0,
                aircraft: flightData.aircraft || '',
                isReturn: flightData.isReturn || false,
                bookingUrl: flightData.bookingUrl || '',
                searchParameters: flightData.searchParameters || {}
            };

            console.log('4. Отправляемые данные для добавления:', favoriteData);

            response = await fetch('/api/favorites/flights', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                credentials: 'include',
                body: JSON.stringify(favoriteData)
            });
        }

        console.log('5. Статус ответа операции:', response.status);

        if (!response.ok) {
            let errorMessage = `HTTP ошибка: ${response.status}`;
            try {
                const errorData = await response.json();
                errorMessage = errorData.message || errorMessage;
            } catch (e) { }
            throw new Error(errorMessage);
        }

        const result = await response.json();
        console.log('6. Результат операции:', result);

        if (result.success) {
            console.log('7. Операция успешна, обновляем кнопку');
            updateFavoriteButton(flightId, !checkData.isFavorite);
            showNotification(
                checkData.isFavorite ? 'Рейс удален из избранного' : 'Рейс добавлен в избранное!',
                'success'
            );
        } else {
            throw new Error(result.message || 'Ошибка при выполнении операции');
        }

    } catch (error) {
        console.error('❌ Ошибка при работе с избранным:', error);
        showNotification(error.message || 'Ошибка при сохранении рейса', 'danger');

        // В случае ошибки синхронизируем статус
        setTimeout(() => {
            syncFavoriteStatus();
        }, 1000);
    } finally {
        // Разблокируем кнопку
        const button = document.querySelector(`[data-flight-id="${CSS.escape(flightData.flightId)}"]`);
        if (button) {
            button.style.pointerEvents = '';
            button.style.opacity = '';
        }
    }
}

// Функция для очистки дубликатов в избранном
async function cleanupDuplicateFavorites() {
    if (!isUserAuthenticated) return;

    console.log('Очистка дубликатов в избранном...');

    try {
        // Получаем все избранные рейсы
        const response = await fetch('/api/favorites/flights', {
            credentials: 'include',
            headers: { 'Accept': 'application/json' }
        });

        if (response.ok) {
            const data = await response.json();
            let favorites = [];

            if (data.favorites) favorites = data.favorites;
            else if (Array.isArray(data)) favorites = data;

            console.log('Всего избранных рейсов:', favorites.length);

            // Группируем по flightId
            const grouped = {};
            favorites.forEach(fav => {
                if (!grouped[fav.flightId]) {
                    grouped[fav.flightId] = [];
                }
                grouped[fav.flightId].push(fav);
            });

            // Находим дубликаты
            const duplicates = [];
            Object.keys(grouped).forEach(flightId => {
                if (grouped[flightId].length > 1) {
                    duplicates.push({
                        flightId: flightId,
                        count: grouped[flightId].length,
                        ids: grouped[flightId].map(f => f.id)
                    });
                }
            });

            console.log('Найдено дубликатов:', duplicates.length);

            // Удаляем дубликаты (оставляем только первый)
            for (const dup of duplicates) {
                // Пропускаем первый элемент, удаляем остальные
                for (let i = 1; i < dup.ids.length; i++) {
                    console.log(`Удаление дубликата ${dup.flightId} (ID: ${dup.ids[i]})`);

                    await fetch(`/api/favorites/flights/${dup.ids[i]}`, {
                        method: 'DELETE',
                        credentials: 'include',
                        headers: {
                            'Content-Type': 'application/json',
                            'Accept': 'application/json'
                        }
                    });
                }
            }

            if (duplicates.length > 0) {
                console.log('Дубликаты удалены, синхронизируем...');
                setTimeout(() => syncFavoriteStatus(), 500);
            }
        }
    } catch (error) {
        console.error('Ошибка при очистке дубликатов:', error);
    }
}

function updateFavoriteButton(flightId, isFavorite) {
    const buttons = document.querySelectorAll(`[data-flight-id="${CSS.escape(flightId)}"]`);
    console.log(`Обновление ${buttons.length} кнопок для рейса ${flightId} в состояние ${isFavorite ? 'избранное' : 'не избранное'}`);

    buttons.forEach(button => {
        const icon = button.querySelector('i');
        if (icon) {
            if (isFavorite) {
                icon.className = 'fas fa-heart text-danger fa-lg';
                button.title = 'Удалить из избранного';
                button.classList.add('favorited');
            } else {
                icon.className = 'far fa-heart fa-lg text-muted';
                button.title = 'Добавить в избранное';
                button.classList.remove('favorited');
            }
        }
    });
}

// ==================== ПОИСК РЕЙСОВ ====================
async function searchFlights(searchData) {
    try {
        console.log('Отправка запроса к API:', searchData);
        const response = await fetch('/api/flights/search', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(searchData)
        });

        console.log('Статус ответа:', response.status);
        if (!response.ok) {
            let errorText = `HTTP error! status: ${response.status}`;
            try {
                const errorData = await response.json();
                errorText = errorData.error || errorData.message || errorText;
            } catch (e) {
                console.log('Не удалось распарсить ошибку:', e);
            }
            throw new Error(errorText);
        }

        const result = await response.json();
        console.log('Ответ от API:', result);
        return result;
    } catch (error) {
        console.error('Ошибка при поиске рейсов:', error);
        throw error;
    }
}

// ==================== ПОКАЗ РЕЗУЛЬТАТОВ ====================
// ==================== ПОКАЗ РЕЗУЛЬТАТОВ ====================
function showFlightResults(flights, searchData) {
    console.log('=== ПОКАЗ РЕЗУЛЬТАТОВ ===');
    console.log('Получено рейсов:', flights?.length || 0);
    console.log('Первый рейс:', flights[0]);
    console.log('Пользователь авторизован:', isUserAuthenticated);

    // Уникальный ID для текущего поиска
    const searchId = Date.now() + Math.random().toString(36);
    window.currentSearchId = searchId;
    console.log('ID поиска:', searchId);

    // Очищаем предыдущие результаты
    const oldResults = document.getElementById('flightResultsContainer');
    if (oldResults) oldResults.innerHTML = '';

    // Отменяем предыдущие проверки избранного
    if (window.favoriteCheckTimeout) {
        clearTimeout(window.favoriteCheckTimeout);
        window.favoriteCheckTimeout = null;
    }

    if (!flights || flights.length === 0) {
        document.getElementById('flightResultsContainer').innerHTML = `
            <div class="alert alert-info mt-4">
                <h5 class="alert-heading">Рейсы не найдены</h5>
                <p>Попробуйте изменить параметры поиска или даты</p>
            </div>
        `;
        return;
    }

    // Разделяем рейсы на туда и обратно
    const oneWayFlights = flights.filter(flight => !flight.isReturn);
    const returnFlights = flights.filter(flight => flight.isReturn);
    const hasReturnDate = searchData.returnDate && searchData.returnDate !== null && searchData.returnDate !== '';

    let html = `
        <div class="card shadow-lg border-0 mb-4 mt-4">
            <div class="card-header bg-primary text-white py-3">
                <div class="d-flex justify-content-between align-items-center">
                    <h4 class="mb-0">
                        <i class="fas fa-plane me-2"></i>
                        Найдено рейсов: <span class="badge bg-light text-primary">${flights.length}</span>
                    </h4>
                    <div class="text-end">
                        <small class="d-block">${hasReturnDate ? 'Туда и обратно' : 'В одну сторону'}</small>
                        <small class="d-block">${searchData.passengers} пассажир${searchData.passengers > 1 ? 'а' : ''}</small>
                    </div>
                </div>
            </div>
            <div class="card-body p-0">
    `;

    // Рейсы туда
    if (oneWayFlights.length > 0) {
        html += `
            <div class="flight-section-header section-tuda p-3">
                <div class="d-flex align-items-center">
                    <i class="fas fa-plane-departure fa-2x text-primary me-3"></i>
                    <div>
                        <h5 class="mb-1 fw-bold">Рейсы туда</h5>
                        <p class="mb-0 text-muted">
                            ${searchData.departureCity} → ${searchData.arrivalCity}
                            <span class="ms-2 badge bg-primary">${formatDateForDisplay(searchData.departureDate)}</span>
                        </p>
                    </div>
                </div>
            </div>
        `;
        oneWayFlights.forEach((flight, index) => {
            html += generateFlightCard(flight, index, false);
        });
    }

    // Рейсы обратно
    if (hasReturnDate && returnFlights.length > 0) {
        html += `
            <div class="flight-section-header section-obratno p-3">
                <div class="d-flex align-items-center">
                    <i class="fas fa-plane-arrival fa-2x text-success me-3"></i>
                    <div>
                        <h5 class="mb-1 fw-bold">Рейсы обратно</h5>
                        <p class="mb-0 text-muted">
                            ${searchData.arrivalCity} → ${searchData.departureCity}
                            <span class="ms-2 badge bg-success">${formatDateForDisplay(searchData.returnDate)}</span>
                        </p>
                    </div>
                </div>
            </div>
        `;
        returnFlights.forEach((flight, index) => {
            html += generateFlightCard(flight, index, true);
        });
    }

    html += `
            </div>
            <div class="card-footer bg-light py-3">
                <div class="row">
                    <div class="col-md-6">
                        <small class="text-muted">
                            <i class="fas fa-info-circle me-1"></i>
                            Все цены указаны в рублях (демо-данные)
                        </small>
                    </div>
                    <div class="col-md-6 text-end">
                        <small class="text-muted">
                            <i class="fas fa-sync-alt me-1"></i>
                            Данные обновлены: ${new Date().toLocaleTimeString('ru-RU')}
                        </small>
                    </div>
                </div>
            </div>
        </div>
    `;

    // Вставляем HTML
    document.getElementById('flightResultsContainer').innerHTML = html;

    // Назначаем обработчики для кнопок избранного
    const favoriteButtons = document.querySelectorAll('.favorite-btn');
    favoriteButtons.forEach(button => {
        // Удаляем старые обработчики
        button.removeEventListener('click', handleFavoriteClick);
        // Добавляем новый обработчик
        button.addEventListener('click', function (e) {
            e.preventDefault();
            e.stopPropagation();
            handleFavoriteClick(this);
        });
    });

    // Если пользователь авторизован, проверяем статус избранного
    if (isUserAuthenticated) {
        // Сначала сбрасываем все кнопки в состояние "не в избранном"
        favoriteButtons.forEach(button => {
            const flightId = button.getAttribute('data-flight-id');
            if (flightId) {
                const icon = button.querySelector('i');
                if (icon) {
                    icon.className = 'far fa-heart fa-lg text-muted';
                    button.title = 'Добавить в избранное';
                    button.classList.remove('favorited');
                }
            }
        });

        // Запускаем проверку с задержкой, чтобы убедиться, что DOM обновился
        window.favoriteCheckTimeout = setTimeout(() => {
            // Проверяем, не устарел ли поиск
            if (window.currentSearchId === searchId) {
                checkFavoritesForFlights();
            }
        }, 500);
    }

    // Плавный скролл к результатам
    setTimeout(() => {
        const resultsElement = document.getElementById('flightResultsContainer');
        if (resultsElement) {
            resultsElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }, 200);

    console.log('Результаты отображены, ожидаем проверку избранного...');
}

function buildStableFlightId(flight, isReturnFlight) {
    // Используем только безопасные символы для URL
    const airline = (flight.airlineCode || flight.airline || 'FLT').replace(/[^a-zA-Z0-9]/g, '');
    const flightNum = (flight.flightNumber || '000').replace(/[^a-zA-Z0-9]/g, '');
    const from = (flight.departureCity || 'FROM').replace(/[^a-zA-Z0-9]/g, '');
    const to = (flight.arrivalCity || 'TO').replace(/[^a-zA-Z0-9]/g, '');

    // Используем timestamp даты для уникальности
    const departureDate = flight.departureTime ? new Date(flight.departureTime).toISOString().split('T')[0].replace(/-/g, '') : '000000';

    // Создаем уникальный ID без специальных символов
    const baseId = `${airline}_${flightNum}_${from}_${to}_${departureDate}_${isReturnFlight ? 'R' : 'O'}`;

    // Добавляем хеш для уникальности
    let hash = 0;
    for (let i = 0; i < baseId.length; i++) {
        hash = ((hash << 5) - hash) + baseId.charCodeAt(i);
        hash = hash & hash;
    }

    // Возвращаем ID только с безопасными символами
    return `FLT_${Math.abs(hash).toString(36)}_${isReturnFlight ? 'R' : 'O'}`;
}

function generateFlightCard(flight, index, isReturnFlight) {
    if (!flight) return '';

    console.log('Генерация карточки для рейса:', flight);
    console.log('DepartureCity:', flight.departureCity);
    console.log('ArrivalCity:', flight.arrivalCity);
    console.log('DepartureAirport:', flight.departureAirport);
    console.log('ArrivalAirport:', flight.arrivalAirport);

    const departureTime = formatTime(flight.departureTime);
    const arrivalTime = formatTime(flight.arrivalTime);
    const durationHours = Math.floor(flight.duration / 60);
    const durationMinutes = flight.duration % 60;
    const durationText = `${durationHours}ч ${durationMinutes}м`;

    const flightId = buildStableFlightId(flight, isReturnFlight);
    const typeColor = isReturnFlight ? 'success' : 'primary';
    const typeIcon = isReturnFlight ? 'fa-plane-arrival' : 'fa-plane-departure';
    const typeClass = isReturnFlight ? 'return-flight' : 'oneway-flight';
    const priceFormatted = flight.price ? flight.price.toLocaleString('ru-RU') : '0';
    const currency = flight.currency || 'RUB';

    // ВАЖНО: Проверяем, что попадает в flightData
    // В функции generateFlightCard, после создания flightData добавьте проверку:
    const flightData = {
        flightId: flightId,
        airline: flight.airline || 'Авиакомпания',
        airlineCode: flight.airlineCode || '',
        flightNumber: flight.flightNumber || 'Рейс',
        departureCity: flight.departureCity || '',
        arrivalCity: flight.arrivalCity || '',
        departureAirport: flight.departureAirport || '',
        arrivalAirport: flight.arrivalAirport || '',
        departureTime: flight.departureTime,
        arrivalTime: flight.arrivalTime,
        price: flight.price || 0,
        currency: flight.currency || 'RUB',
        transfers: flight.transfers || 0,
        duration: flight.duration || 0,
        aircraft: flight.aircraft || '',
        isReturn: isReturnFlight,
        bookingUrl: flight.bookingUrl || '#',
        searchParameters: {
            departureCity: flight.departureCity,
            arrivalCity: flight.arrivalCity,
            departureDate: formatDateForApi(flight.departureTime)
        }
    };

    // ВАЖНО: Экранируем кавычки для безопасного вставления в HTML
    const flightDataJson = JSON.stringify(flightData).replace(/'/g, "&apos;");

    console.log('flightData для карточки:', flightData);

    return `
        <div class="flight-card ${typeClass} border-bottom p-4">
            <div class="row align-items-center">
                <div class="col-md-2">
                    <div class="d-flex align-items-center">
                        <i class="fas ${typeIcon} text-${typeColor} fa-lg me-3"></i>
                        <div>
                            <h6 class="mb-1 fw-bold">${flight.airline || 'Авиакомпания'}</h6>
                            <small class="text-muted">${flight.flightNumber || 'Рейс'}</small>
                        </div>
                    </div>
                </div>

                <div class="col-md-5">
                    <div class="row align-items-center">
                        <div class="col-4 text-end">
                            <div class="fw-bold fs-5 time-display text-${typeColor}">${departureTime}</div>
                            <small class="text-muted d-block">${flight.departureAirport || ''}</small>
                            <small class="text-muted">${flight.departureCity || ''}</small>
                        </div>

                        <div class="col-4 text-center">
                            <div class="flight-duration position-relative">
                                <div class="position-relative">
                                    <i class="fas fa-plane text-${typeColor} fa-lg"></i>
                                </div>
                                <div class="small text-muted mt-2">${durationText}</div>
                                ${flight.transfers > 0 ?
            `<div class="transfer-info small mt-1">${flight.transfers} пересад${flight.transfers === 1 ? 'ка' : 'ки'}</div>` :
            '<div class="text-success small mt-1">Прямой рейс</div>'
        }
                            </div>
                        </div>

                        <div class="col-4">
                            <div class="fw-bold fs-5 time-display text-${typeColor}">${arrivalTime}</div>
                            <small class="text-muted d-block">${flight.arrivalAirport || ''}</small>
                            <small class="text-muted">${flight.arrivalCity || ''}</small>
                        </div>
                    </div>
                </div>

                <div class="col-md-1 text-center">
                    <span class="badge ${flight.transfers === 0 ? 'bg-success' : 'bg-warning'} fs-6">
                        ${flight.transfers === 0 ? 'Прямой' : `${flight.transfers}`}
                    </span>
                </div>

                <div class="col-md-4">
                    <div class="d-flex align-items-center justify-content-end gap-3">
                        <button class="favorite-btn p-2 border-0 bg-transparent"
                                data-flight-id="${flightId}"
                                data-flight-data='${JSON.stringify(flightData).replace(/'/g, "&apos;")}'
                                title="${isUserAuthenticated ? 'Добавить в избранное' : 'Войдите для сохранения'}"
                                style="transition: transform 0.2s;"
                                onclick="handleFavoriteClick(this)">
                            <i class="far fa-heart fa-lg text-muted"></i>
                        </button>

                        <div class="text-end">
                            <div class="d-flex align-items-baseline justify-content-end">
                                <h3 class="text-${typeColor} mb-0">${priceFormatted}</h3>
                                <span class="text-${typeColor} ms-1">${currency}</span>
                            </div>
                            <small class="text-muted d-block">за пассажира</small>
                            <button class="btn btn-${typeColor} btn-lg px-4 mt-2 fw-bold"
                                    onclick="selectRealFlight('${flightId}', ${flight.price || 0}, '${flight.airline || ''}', ${isReturnFlight})">
                                <i class="fas fa-shopping-cart me-2"></i>Купить
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;
}

// ==================== ОБРАБОТКА ИЗБРАННОГО ====================
async function handleFavoriteClick(button) {
    // Предотвращаем множественные клики
    if (button.disabled) return;

    button.disabled = true;

    const flightId = button.getAttribute('data-flight-id');
    const flightDataStr = button.getAttribute('data-flight-data');

    if (!flightDataStr) {
        console.error('Данные рейса не найдены');
        button.disabled = false;
        return;
    }

    try {
        // Правильно парсим данные, заменяя &apos; обратно на кавычки
        const flightData = JSON.parse(flightDataStr.replace(/&apos;/g, "'"));
        console.log('Обработка клика по избранному:', flightData);
        await toggleFavorite(flightData);
    } catch (error) {
        console.error('Ошибка при обработке данных рейса:', error);
        showNotification('Ошибка при сохранении рейса', 'danger');
    } finally {
        // Разблокируем кнопку через небольшую задержку
        setTimeout(() => {
            button.disabled = false;
        }, 500);
    }
}

async function checkFavoritesForFlights() {
    if (!isUserAuthenticated || isCheckingFavorites) return;

    const buttons = document.querySelectorAll('.favorite-btn');
    console.log('Проверяем избранное для', buttons.length, 'кнопок');

    isCheckingFavorites = true;

    // Собираем все flightId
    const flightIds = [];
    buttons.forEach(button => {
        const flightId = button.getAttribute('data-flight-id');
        if (flightId) flightIds.push(flightId);
    });

    console.log('Flight IDs для проверки:', flightIds);

    // Создаем карту для хранения результатов
    const favoriteStatus = new Map();

    // Проверяем рейсы с небольшой задержкой между запросами
    for (let i = 0; i < flightIds.length; i++) {
        const flightId = flightIds[i];

        // Проверяем, не устарел ли поиск
        if (!window.currentSearchId) {
            console.log('Поиск устарел, прерываем проверку');
            break;
        }

        // Добавляем небольшую задержку между запросами
        await new Promise(resolve => setTimeout(resolve, 100));

        try {
            console.log(`Проверка рейса ${i + 1}/${flightIds.length}: ${flightId}`);

            // Правильно кодируем flightId для URL
            const encodedFlightId = encodeURIComponent(flightId);
            const url = `/api/favorites/flights/check/${encodedFlightId}?t=${Date.now()}`;
            console.log('URL проверки:', url);

            const response = await fetch(url, {
                credentials: 'include',
                headers: {
                    'Accept': 'application/json',
                    'Cache-Control': 'no-cache'
                }
            });

            if (response.ok) {
                const data = await response.json();
                console.log(`Результат для ${flightId}:`, data);

                if (data.isFavorite) {
                    favoriteStatus.set(flightId, true);
                    console.log(`✅ Рейс ${flightId} В избранном`);
                } else {
                    favoriteStatus.set(flightId, false);
                    console.log(`❌ Рейс ${flightId} НЕ в избранном`);
                }
            } else {
                console.error('Ошибка ответа для:', flightId, response.status);
                // Пытаемся получить текст ошибки
                try {
                    const errorText = await response.text();
                    console.error('Текст ошибки:', errorText);
                } catch (e) {
                    console.error('Не удалось получить текст ошибки');
                }
            }
        } catch (error) {
            console.error('Ошибка проверки для рейса:', flightId, error);
        }
    }

    // Обновляем все кнопки согласно полученным результатам
    console.log('Итоговый статус избранного:', Object.fromEntries(favoriteStatus));

    flightIds.forEach(flightId => {
        const isFavorite = favoriteStatus.get(flightId) || false;
        updateFavoriteButton(flightId, isFavorite);
    });

    isCheckingFavorites = false;
    console.log('Проверка избранного завершена');
}

// Функция для принудительной синхронизации состояния избранного
async function syncFavoriteStatus() {
    if (!isUserAuthenticated) return;

    console.log('Принудительная синхронизация избранного...');

    try {
        // Получаем все избранные рейсы с сервера
        const response = await fetch('/api/favorites/flights', {
            credentials: 'include',
            headers: { 'Accept': 'application/json' }
        });

        if (response.ok) {
            const data = await response.json();
            console.log('Избранные рейсы с сервера:', data);

            // Создаем Set избранных flightId
            const favoriteIds = new Set();
            if (data.favorites) {
                data.favorites.forEach(f => favoriteIds.add(f.flightId));
            } else if (Array.isArray(data)) {
                data.forEach(f => favoriteIds.add(f.flightId));
            }

            console.log('Избранные ID:', Array.from(favoriteIds));

            // Обновляем все кнопки
            const buttons = document.querySelectorAll('.favorite-btn');
            buttons.forEach(button => {
                const flightId = button.getAttribute('data-flight-id');
                if (flightId) {
                    updateFavoriteButton(flightId, favoriteIds.has(flightId));
                }
            });

            console.log('Синхронизация завершена');
        }
    } catch (error) {
        console.error('Ошибка при синхронизации:', error);
    }
}

// ==================== ПОКУПКА БИЛЕТОВ ====================
function selectRealFlight(flightId, price, airline, isReturn) {
    console.log('========== НАЧАЛО БРОНИРОВАНИЯ ==========');
    console.log('Параметры вызова:', { flightId, price, airline, isReturn });

    // Находим карточку рейса
    const flightCard = event.currentTarget.closest('.flight-card');
    if (!flightCard) {
        console.error('❌ Карточка рейса не найдена');
        return;
    }
    console.log('✅ Карточка рейса найдена');

    // Получаем данные из data-flight-data
    const favoriteButton = flightCard.querySelector('.favorite-btn');
    if (!favoriteButton) {
        console.error('❌ Кнопка избранного не найдена');
        return;
    }
    console.log('✅ Кнопка избранного найдена');

    const flightDataStr = favoriteButton.getAttribute('data-flight-data');
    if (!flightDataStr) {
        console.error('❌ Данные рейса не найдены');
        return;
    }

    try {
        // Парсим данные рейса
        const flightData = JSON.parse(flightDataStr.replace(/&apos;/g, "'"));
        console.log('✅ Данные из flightData:', flightData);

        // Получаем дату из формы поиска
        const departureDateInput = document.getElementById('departureDate');
        const returnDateInput = document.getElementById('returnDate');

        // Формируем даты с правильным временем
        let departureDateTime = new Date(flightData.departureTime);
        let arrivalDateTime = new Date(flightData.arrivalTime);

        // Если дата невалидна, используем текущую + завтра
        if (isNaN(departureDateTime.getTime())) {
            departureDateTime = new Date();
            departureDateTime.setDate(departureDateTime.getDate() + 1);
            departureDateTime.setHours(10, 0, 0, 0);
        }

        if (isNaN(arrivalDateTime.getTime())) {
            arrivalDateTime = new Date(departureDateTime);
            arrivalDateTime.setHours(departureDateTime.getHours() + 2,
                departureDateTime.getMinutes(), 0, 0);
        }

        // Формируем данные для бронирования, используя данные из flightData
        const bookingData = {
            flightId: flightId,
            airline: flightData.airline || airline,
            airlineCode: flightData.airlineCode || flightId.split('_')[0] || 'SU',
            airlineLogo: flightData.airlineLogo || '',
            flightNumber: flightData.flightNumber || flightId.split('_')[1] || 'SU 1234',
            departureCity: flightData.departureCity || '',
            arrivalCity: flightData.arrivalCity || '',
            departureAirport: flightData.departureAirport || '',
            arrivalAirport: flightData.arrivalAirport || '',
            departureDateTime: departureDateTime.toISOString(),
            arrivalDateTime: arrivalDateTime.toISOString(),
            price: price,
            duration: flightData.duration || 120,
            transfers: flightData.transfers || 0,
            aircraft: flightData.aircraft || 'Airbus A320',
            baggage: '1x23кг',
            handLuggage: '1x10кг',
            meal: 'Включено',
            flightClass: 'economy',
            isRoundTrip: isReturn,
            passengers: 1
        };

        console.log('✅ Итоговые данные для бронирования:', bookingData);

        // Формируем URL
        const params = new URLSearchParams();
        for (const [key, value] of Object.entries(bookingData)) {
            if (value !== null && value !== undefined && value !== '') {
                params.append(key, value.toString());
            }
        }

        const url = `/FlightBooking/Book?${params.toString()}`;
        console.log('✅ URL для перехода:', url);
        console.log('========== КОНЕЦ БРОНИРОВАНИЯ ==========');

        window.location.href = url;

    } catch (error) {
        console.error('❌ Ошибка при подготовке данных:', error);
        showNotification('Ошибка при подготовке данных для бронирования', 'danger');
    }
}

function formatDateForUrl(date) {
    if (!date) return '';
    try {
        const d = new Date(date);
        return d.toISOString();
    } catch (error) {
        console.error('Ошибка форматирования даты:', error);
        return '';
    }
}

// ==================== МОИ ЗАКАЗЫ ====================
function showMyOrders() {
    if (!isUserAuthenticated) {
        showAuthRequiredModal();
        return;
    }

    fetch('/api/flights/my-orders', { credentials: 'include' })
        .then(response => {
            if (response.ok) return response.json();
            throw new Error('Не удалось загрузить заказы');
        })
        .then(data => displayOrdersModal(data.orders || []))
        .catch(error => {
            console.error('Ошибка при получении заказов:', error);
            alert('Ошибка при загрузке заказов');
        });
}

function displayOrdersModal(orders) {
    let ordersHtml = '';
    if (orders.length === 0) {
        ordersHtml = `
            <div class="text-center py-5">
                <i class="fas fa-ticket-alt fa-4x text-muted mb-3"></i>
                <h4>У вас пока нет заказов</h4>
                <p class="text-muted">Начните поиск рейсов и сделайте свой первый заказ!</p>
                <button class="btn btn-primary mt-3" data-bs-dismiss="modal">Найти рейсы</button>
            </div>
        `;
    } else {
        ordersHtml = orders.map(order => `
            <div class="card mb-3">
                <div class="card-header d-flex justify-content-between align-items-center">
                    <div>
                        <strong>Заказ #${order.orderNumber}</strong>
                        <span class="badge ${getStatusBadgeClass(order.status)} ms-2">${getStatusText(order.status)}</span>
                    </div>
                    <small class="text-muted">${new Date(order.createdAt).toLocaleDateString('ru-RU')}</small>
                </div>
                <div class="card-body">
                    <div class="row">
                        <div class="col-md-6">
                            <p class="mb-1"><strong>Рейс:</strong> ${order.flightNumber}</p>
                            <p class="mb-1"><strong>Маршрут:</strong> ${order.departureCity} → ${order.arrivalCity}</p>
                            <p class="mb-1"><strong>Вылет:</strong> ${new Date(order.departureTime).toLocaleString('ru-RU')}</p>
                        </div>
                        <div class="col-md-6">
                            <p class="mb-1"><strong>Цена:</strong> ${order.price.toLocaleString('ru-RU')} ${order.currency}</p>
                            <p class="mb-1"><strong>Пассажиров:</strong> ${order.passengers?.length || 1}</p>
                            <p class="mb-1"><strong>Билет:</strong> ${order.ticketNumber || 'ожидает выписки'}</p>
                        </div>
                    </div>
                    <div class="mt-3">
                        <button class="btn btn-sm btn-outline-primary me-2" onclick="viewOrderDetails('${order.id}')">
                            <i class="fas fa-eye me-1"></i>Подробнее
                        </button>
                        ${order.status === 'confirmed' ? `
                            <button class="btn btn-sm btn-outline-success" onclick="printDemoTicket('${order.ticketNumber || 'DEMO-001'}')">
                                <i class="fas fa-print me-1"></i>Печать билета
                            </button>
                        ` : ''}
                        ${order.status === 'pending' ? `
                            <button class="btn btn-sm btn-outline-danger" onclick="cancelOrder('${order.id}')">
                                <i class="fas fa-times me-1"></i>Отменить
                            </button>
                        ` : ''}
                    </div>
                </div>
            </div>
        `).join('');
    }

    const modalHtml = `
        <div class="modal fade" id="ordersModal" tabindex="-1">
            <div class="modal-dialog modal-lg modal-dialog-centered">
                <div class="modal-content">
                    <div class="modal-header bg-primary text-white">
                        <h5 class="modal-title">
                            <i class="fas fa-history me-2"></i>
                            Мои заказы (${orders.length})
                        </h5>
                        <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body">${ordersHtml}</div>
                </div>
            </div>
        </div>
    `;

    document.body.insertAdjacentHTML('beforeend', modalHtml);
    const ordersModal = new bootstrap.Modal(document.getElementById('ordersModal'));
    ordersModal.show();

    document.getElementById('ordersModal').addEventListener('hidden.bs.modal', function () {
        this.remove();
    });
}

function getStatusBadgeClass(status) {
    switch (status) {
        case 'confirmed': return 'bg-success';
        case 'pending': return 'bg-warning';
        case 'cancelled': return 'bg-danger';
        default: return 'bg-secondary';
    }
}

function getStatusText(status) {
    switch (status) {
        case 'confirmed': return 'Подтвержден';
        case 'pending': return 'Ожидает оплаты';
        case 'cancelled': return 'Отменен';
        default: return status;
    }
}

function viewOrderDetails(orderId) {
    fetch(`/api/flights/order/${orderId}`, { credentials: 'include' })
        .then(response => {
            if (response.ok) return response.json();
            throw new Error('Не удалось загрузить детали заказа');
        })
        .then(data => showOrderDetailsModal(data.order))
        .catch(error => console.error('Ошибка при получении деталей заказа:', error));
}

function showOrderDetailsModal(order) {
    const passengersHtml = order.passengers.map(p => `
        <tr>
            <td>${p.lastName} ${p.firstName} ${p.middleName || ''}</td>
            <td>${new Date(p.dateOfBirth).toLocaleDateString('ru-RU')}</td>
            <td>${p.documentNumber}</td>
            <td>${p.seatNumber || '-'}</td>
        </tr>
    `).join('');

    const modalHtml = `
        <div class="modal fade" id="orderDetailsModal" tabindex="-1">
            <div class="modal-dialog modal-lg modal-dialog-centered">
                <div class="modal-content">
                    <div class="modal-header bg-info text-white">
                        <h5 class="modal-title">
                            <i class="fas fa-file-invoice me-2"></i>
                            Заказ #${order.orderNumber}
                        </h5>
                        <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body">
                        <div class="row mb-4">
                            <div class="col-md-6">
                                <h6>Информация о рейсе</h6>
                                <p><strong>Авиакомпания:</strong> ${order.airline}</p>
                                <p><strong>Рейс:</strong> ${order.flightNumber}</p>
                                <p><strong>Вылет:</strong> ${new Date(order.departureTime).toLocaleString('ru-RU')}</p>
                                <p><strong>Прилет:</strong> ${new Date(order.arrivalTime).toLocaleString('ru-RU')}</p>
                            </div>
                            <div class="col-md-6">
                                <h6>Детали заказа</h6>
                                <p><strong>Статус:</strong> <span class="badge ${getStatusBadgeClass(order.status)}">${getStatusText(order.status)}</span></p>
                                <p><strong>Создан:</strong> ${new Date(order.createdAt).toLocaleString('ru-RU')}</p>
                                <p><strong>Билет:</strong> ${order.ticketNumber || '—'}</p>
                                <p><strong>Сумма:</strong> ${order.price.toLocaleString('ru-RU')} ${order.currency}</p>
                            </div>
                        </div>

                        <h6>Пассажиры</h6>
                        <div class="table-responsive">
                            <table class="table table-striped">
                                <thead>
                                    <tr>
                                        <th>ФИО</th>
                                        <th>Дата рождения</th>
                                        <th>Документ</th>
                                        <th>Место</th>
                                    </tr>
                                </thead>
                                <tbody>${passengersHtml}</tbody>
                            </table>
                        </div>

                        <div class="alert alert-info mt-3">
                            <i class="fas fa-info-circle me-2"></i>
                            Демо-заказ. Все данные сгенерированы автоматически.
                        </div>
                    </div>
                    <div class="modal-footer">
                        ${order.status === 'confirmed' && order.ticketNumber ? `
                            <button class="btn btn-primary" onclick="printDemoTicket('${order.ticketNumber}')">
                                <i class="fas fa-print me-1"></i>Печать билета
                            </button>
                        ` : ''}
                        <button class="btn btn-secondary" data-bs-dismiss="modal">Закрыть</button>
                    </div>
                </div>
            </div>
        </div>
    `;

    document.body.insertAdjacentHTML('beforeend', modalHtml);
    const modal = new bootstrap.Modal(document.getElementById('orderDetailsModal'));
    modal.show();

    document.getElementById('orderDetailsModal').addEventListener('hidden.bs.modal', function () {
        this.remove();
    });
}

function cancelOrder(orderId) {
    if (!confirm('Вы уверены, что хотите отменить этот заказ?')) return;

    fetch(`/api/flights/order/${orderId}/cancel`, {
        method: 'POST',
        credentials: 'include'
    })
        .then(response => {
            if (response.ok) return response.json();
            throw new Error('Не удалось отменить заказ');
        })
        .then(data => {
            if (data.success) {
                alert('Заказ успешно отменен!');
                bootstrap.Modal.getInstance(document.getElementById('ordersModal')).hide();
                setTimeout(() => showMyOrders(), 300);
            }
        })
        .catch(error => {
            console.error('Ошибка при отмене заказа:', error);
            alert('Не удалось отменить заказ');
        });
}

// ==================== ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ====================
function extractCityName(fullString) {
    if (!fullString) return '';
    let cityName = fullString.replace(/\([^)]*\)/g, '').trim();
    return cityName || fullString;
}

function formatTime(dateTimeString) {
    if (!dateTimeString) return '--:--';
    try {
        let date = typeof dateTimeString === 'string' ? new Date(dateTimeString) : dateTimeString;
        if (isNaN(date.getTime())) return '--:--';
        return date.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
    } catch (error) {
        console.error('Ошибка форматирования времени:', error, dateTimeString);
        return '--:--';
    }
}

function formatDateForDisplay(dateString) {
    if (!dateString) return '';
    try {
        const date = new Date(dateString);
        if (isNaN(date.getTime())) return '';
        return date.toLocaleDateString('ru-RU', {
            weekday: 'short',
            day: 'numeric',
            month: 'long',
            year: 'numeric'
        });
    } catch (error) {
        console.error('Ошибка форматирования даты:', error, dateString);
        return '';
    }
}

function formatDateForApi(date) {
    if (!date) return '';
    try {
        const d = new Date(date);
        return d.toISOString().split('T')[0];
    } catch (error) {
        console.error('Ошибка форматирования даты для API:', error);
        return '';
    }
}

function showAuthRequiredModal() {
    if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
        const modalElement = document.getElementById('authRequiredModal');
        if (modalElement) {
            const modal = new bootstrap.Modal(modalElement);
            modal.show();
        }
    } else {
        alert('Для сохранения рейсов в избранное необходимо авторизоваться.\n\nПерейдите на страницу входа или регистрации.');
        window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(window.location.pathname);
    }
}

function showNotification(message, type = 'info') {
    const oldNotifications = document.querySelectorAll('.notification-alert');
    oldNotifications.forEach(notification => notification.remove());

    const notification = document.createElement('div');
    notification.className = `notification-alert alert alert-${type} alert-dismissible fade show position-fixed`;
    notification.style.cssText = `
        top: 20px;
        right: 20px;
        z-index: 9999;
        min-width: 300px;
    `;

    notification.innerHTML = `
        <div class="d-flex align-items-center">
            <i class="fas ${type === 'success' ? 'fa-check-circle' : 'fa-info-circle'} me-2"></i>
            <div>${message}</div>
            <button type="button" class="btn-close ms-auto" data-bs-dismiss="alert"></button>
        </div>
    `;

    document.body.appendChild(notification);
    setTimeout(() => {
        if (notification.parentElement) notification.remove();
    }, 5000);
}

function debounceSearch(input, dropdown, delay = 300) {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => {
        searchCitiesFromTravelPayouts(input.value, dropdown);
    }, delay);
}

function loadPopularDestinations() {
    try {
        setTimeout(() => {
            const popularDestinationsHTML = `
                <div class="col-md-4">
                    <div class="card destination-card h-100" onclick="selectPopularDestination('Москва', 'Санкт-Петербург')">
                        <img src="https://i.pinimg.com/originals/8e/d6/12/8ed6120ddbb569d44c6c7edaea15cce9.png" class="card-img-top" alt="Москва - Санкт-Петербург">
                        <div class="card-body">
                            <h5 class="card-title">Москва → Санкт-Петербург</h5>
                            <p class="card-text">От 2 500 ₽</p>
                            <p class="text-muted small">В пути от 1ч 30м</p>
                        </div>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="card destination-card h-100" onclick="selectPopularDestination('Москва', 'Сочи')">
                        <img src="https://www.kupibilet.ru/blog/wp-content/uploads/2025/06/sochi-park-1.jpg" class="card-img-top" alt="Москва - Сочи">
                        <div class="card-body">
                            <h5 class="card-title">Москва → Сочи</h5>
                            <p class="card-text">От 4 800 ₽</p>
                            <p class="text-muted small">В пути от 2ч 30м</p>
                        </div>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="card destination-card h-100" onclick="selectPopularDestination('Москва', 'Казань')">
                        <img src="https://cdn.culture.ru/images/a95b2c46-77db-5224-a88b-1079b9f3c3b0" class="card-img-top" alt="Москва - Казань">
                        <div class="card-body">
                            <h5 class="card-title">Москва → Казань</h5>
                            <p class="card-text">От 3 200 ₽</p>
                            <p class="text-muted small">В пути от 1ч 45м</p>
                        </div>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="card destination-card h-100" onclick="selectPopularDestination('Москва', 'Екатеринбург')">
                        <img src="https://sdelanounas.ru/i/a/w/1/f_aW1nLmdlbGlvcGhvdG8uY29tL2VrYjIwMjAvMjhfZWtiMjAyMC5jcGc_X19pZD0xMzY3NTQ=.jpeg" class="card-img-top" alt="Москва - Екатеринбург">
                        <div class="card-body">
                            <h5 class="card-title">Москва → Екатеринбург</h5>
                            <p class="card-text">От 3 800 ₽</p>
                            <p class="text-muted small">В пути от 2ч 15м</p>
                        </div>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="card destination-card h-100" onclick="selectPopularDestination('Москва', 'Краснодар')">
                        <img src="https://blog.ostrovok.ru/wp-content/uploads/2022/05/8-1.jpg" class="card-img-top" alt="Москва - Краснодар">
                        <div class="card-body">
                            <h5 class="card-title">Москва → Краснодар</h5>
                            <p class="card-text">От 4 200 ₽</p>
                            <p class="text-muted small">В пути от 2ч</p>
                        </div>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="card destination-card h-100" onclick="selectPopularDestination('Тюмень', 'Москва')">
                        <img src="https://avatars.mds.yandex.net/get-altay/9888332/2a0000018df4ed7c23145a090d52f79baa93/orig" class="card-img-top" alt="Тюмень - Москва">
                        <div class="card-body">
                            <h5 class="card-title">Тюмень → Москва</h5>
                            <p class="card-text">От 5 500 ₽</p>
                            <p class="text-muted small">В пути от 2ч 30м</p>
                        </div>
                    </div>
                </div>
            `;

            const container = document.getElementById('popularDestinations');
            if (container) container.innerHTML = popularDestinationsHTML;
        }, 1000);
    } catch (error) {
        console.error('❌ Ошибка загрузки популярных направлений:', error);
    }
}

function selectPopularDestination(fromCity, toCity) {
    console.log(`🎯 Выбрано направление: ${fromCity} → ${toCity}`);
    document.getElementById('departureCity').value = `${fromCity} (${getCityCode(fromCity)})`;
    document.getElementById('arrivalCity').value = `${toCity} (${getCityCode(toCity)})`;

    const today = new Date();
    const departureDate = new Date(today);
    departureDate.setDate(today.getDate() + 3);
    const returnDate = new Date(today);
    returnDate.setDate(today.getDate() + 10);

    document.getElementById('departureDate').value = departureDate.toISOString().split('T')[0];
    document.getElementById('returnDate').value = returnDate.toISOString().split('T')[0];

    document.getElementById('flightSearchForm').scrollIntoView({ behavior: 'smooth' });
    showNotification(`Направление ${fromCity} → ${toCity} добавлено в форму поиска`, 'success');
}

function getCityCode(cityName) {
    const cityCodes = {
        "Москва": "MOW", "Санкт-Петербург": "LED", "Сочи": "AER", "Казань": "KZN",
        "Екатеринбург": "SVX", "Краснодар": "KRR", "Минеральные Воды": "MRV",
        "Симферополь": "SIP", "Калининград": "KGD", "Новосибирск": "OVB", "Тюмень": "TJM"
    };
    return cityCodes[cityName] || "---";
}

// ==================== ИНИЦИАЛИЗАЦИЯ ====================
// ==================== ИНИЦИАЛИЗАЦИЯ ====================
async function initializeFlightPage() {
    console.log('Инициализация страницы авиабилетов...');

    const departureInput = document.getElementById('departureCity');
    const arrivalInput = document.getElementById('arrivalCity');
    const departureDropdown = document.getElementById('departureDropdown');
    const arrivalDropdown = document.getElementById('arrivalDropdown');

    // Проверяем авторизацию и синхронизируем избранное
    await checkAuthStatus();

    // Синхронизируем избранное после проверки авторизации
    if (isUserAuthenticated) {
        console.log('Пользователь авторизован,准备 синхронизацию избранного...');
        // Синхронизируем избранное через секунду после загрузки
        setTimeout(() => {
            syncFavoriteStatus();
        }, 1000);
    } else {
        console.log('Пользователь не авторизован');
    }

    // Обработчики автозаполнения для города отправления
    if (departureInput && departureDropdown) {
        departureInput.addEventListener('input', () => {
            currentDropdown = departureDropdown;
            currentInput = departureInput;
            debounceSearch(departureInput, departureDropdown);
        });

        departureInput.addEventListener('focus', () => {
            if (departureInput.value.length >= 2) {
                currentDropdown = departureDropdown;
                currentInput = departureInput;
                searchCitiesFromTravelPayouts(departureInput.value, departureDropdown);
            }
        });

        // Закрытие dropdown при потере фокуса
        departureInput.addEventListener('blur', () => {
            // Небольшая задержка, чтобы успеть кликнуть на элемент
            setTimeout(() => {
                if (!departureDropdown.contains(document.activeElement)) {
                    departureDropdown.style.display = 'none';
                }
            }, 200);
        });
    }

    // Обработчики автозаполнения для города прибытия
    if (arrivalInput && arrivalDropdown) {
        arrivalInput.addEventListener('input', () => {
            currentDropdown = arrivalDropdown;
            currentInput = arrivalInput;
            debounceSearch(arrivalInput, arrivalDropdown);
        });

        arrivalInput.addEventListener('focus', () => {
            if (arrivalInput.value.length >= 2) {
                currentDropdown = arrivalDropdown;
                currentInput = arrivalInput;
                searchCitiesFromTravelPayouts(arrivalInput.value, arrivalDropdown);
            }
        });

        arrivalInput.addEventListener('blur', () => {
            setTimeout(() => {
                if (!arrivalDropdown.contains(document.activeElement)) {
                    arrivalDropdown.style.display = 'none';
                }
            }, 200);
        });
    }

    // Закрытие dropdown при клике вне
    document.addEventListener('click', (e) => {
        if (!e.target.closest('.city-autocomplete')) {
            if (departureDropdown) departureDropdown.style.display = 'none';
            if (arrivalDropdown) arrivalDropdown.style.display = 'none';
        }
    });

    // Навигация с клавиатуры для автодополнения
    document.addEventListener('keydown', (e) => {
        if (!currentDropdown || currentDropdown.style.display === 'none') return;

        const items = currentDropdown.querySelectorAll('.autocomplete-item');
        let activeItem = currentDropdown.querySelector('.autocomplete-item.active');
        let activeIndex = activeItem ? Array.from(items).indexOf(activeItem) : -1;

        switch (e.key) {
            case 'ArrowDown':
                e.preventDefault();
                if (activeIndex < items.length - 1) {
                    if (activeItem) activeItem.classList.remove('active');
                    items[activeIndex + 1].classList.add('active');
                } else if (items.length > 0 && activeIndex === -1) {
                    items[0].classList.add('active');
                }
                break;

            case 'ArrowUp':
                e.preventDefault();
                if (activeIndex > 0) {
                    if (activeItem) activeItem.classList.remove('active');
                    items[activeIndex - 1].classList.add('active');
                }
                break;

            case 'Enter':
                e.preventDefault();
                if (activeItem) {
                    activeItem.click();
                }
                break;

            case 'Escape':
                currentDropdown.style.display = 'none';
                break;
        }
    });

    // Обработка формы поиска
    const flightSearchForm = document.getElementById('flightSearchForm');
    if (flightSearchForm) {
        flightSearchForm.addEventListener('submit', async function (e) {
            e.preventDefault();
            console.log('=== ОТПРАВКА ФОРМЫ ===');

            const departure = document.getElementById('departureCity').value;
            const arrival = document.getElementById('arrivalCity').value;
            const departureDate = document.getElementById('departureDate').value;
            const returnDate = document.getElementById('returnDate').value;
            const passengers = document.getElementById('passengers').value;

            // Валидация
            if (!departure || !arrival) {
                showNotification('Пожалуйста, заполните города вылета и прилета', 'warning');
                return;
            }

            if (!departureDate) {
                showNotification('Пожалуйста, выберите дату вылета', 'warning');
                return;
            }

            // Извлекаем названия городов без кодов
            const departureCity = extractCityName(departure);
            const arrivalCity = extractCityName(arrival);

            const searchData = {
                departureCity: departureCity,
                arrivalCity: arrivalCity,
                departureDate: departureDate,
                passengers: parseInt(passengers),
                class: "economy",
                tripType: returnDate && returnDate.length > 0 ? "round" : "oneway"
            };

            if (returnDate && returnDate.length > 0) {
                searchData.returnDate = returnDate;
            }

            console.log('Параметры поиска:', searchData);

            const submitBtn = this.querySelector('button[type="submit"]');
            const originalText = submitBtn.innerHTML;
            submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Поиск...';
            submitBtn.disabled = true;

            try {
                const result = await searchFlights(searchData);
                if (result.success) {
                    showFlightResults(result.flights, searchData);
                    showNotification(`Найдено ${result.flights?.length || 0} рейсов`, 'success');
                } else {
                    showNotification(result.error || 'Произошла ошибка при поиске', 'danger');
                }
            } catch (error) {
                console.error('Ошибка поиска:', error);
                showNotification(`Ошибка: ${error.message}`, 'danger');
            } finally {
                submitBtn.innerHTML = originalText;
                submitBtn.disabled = false;
            }
        });

        // Установка дат по умолчанию
        const today = new Date();
        const tomorrow = new Date(today);
        tomorrow.setDate(tomorrow.getDate() + 1);
        const tomorrowStr = tomorrow.toISOString().split('T')[0];

        const nextWeek = new Date(today);
        nextWeek.setDate(nextWeek.getDate() + 8);
        const nextWeekStr = nextWeek.toISOString().split('T')[0];

        const departureDateInput = document.getElementById('departureDate');
        const returnDateInput = document.getElementById('returnDate');

        if (departureDateInput) {
            departureDateInput.min = today.toISOString().split('T')[0];
            departureDateInput.value = tomorrowStr;
        }

        if (returnDateInput) {
            returnDateInput.min = tomorrowStr;
            returnDateInput.value = nextWeekStr;
        }

        // Обновление минимальной даты обратного рейса при изменении даты вылета
        if (departureDateInput && returnDateInput) {
            departureDateInput.addEventListener('change', function () {
                returnDateInput.min = this.value;
                if (returnDateInput.value && returnDateInput.value < this.value) {
                    returnDateInput.value = this.value;
                }
            });
        }
    }

    // Добавляем кнопку "Мои заказы" в шапку
    const headerControls = document.querySelector('.container .row .col-12');
    if (headerControls) {
        // Проверяем, не добавлена ли уже кнопка
        if (!document.getElementById('myOrdersBtn')) {
            const ordersBtn = document.createElement('button');
            ordersBtn.id = 'myOrdersBtn';
            ordersBtn.className = 'btn btn-outline-primary mb-4 me-2';
            ordersBtn.innerHTML = '<i class="fas fa-ticket-alt me-2"></i>Мои заказы';
            ordersBtn.onclick = showMyOrders;

            // Вставляем после заголовка
            const title = headerControls.querySelector('h1');
            if (title) {
                title.insertAdjacentElement('afterend', ordersBtn);
            } else {
                headerControls.insertBefore(ordersBtn, headerControls.firstChild);
            }
        }
    }

    // Загружаем популярные направления
    loadPopularDestinations();

    // Добавляем обработчик для обновления статуса избранного при возвращении на страницу
    window.addEventListener('focus', () => {
        if (isUserAuthenticated) {
            console.log('Страница в фокусе, проверяем избранное...');
            syncFavoriteStatus();
        }
    });

    console.log('Инициализация страницы авиабилетов завершена');
}

// ==================== ФУНКЦИЯ СИНХРОНИЗАЦИИ ИЗБРАННОГО ====================
async function syncFavoriteStatus() {
    if (!isUserAuthenticated) {
        console.log('Пользователь не авторизован, синхронизация не требуется');
        return;
    }

    console.log('Принудительная синхронизация избранного...');

    try {
        // Получаем все избранные рейсы с сервера
        const response = await fetch('/api/favorites/flights', {
            credentials: 'include',
            headers: {
                'Accept': 'application/json',
                'Cache-Control': 'no-cache'
            }
        });

        if (response.ok) {
            const data = await response.json();
            console.log('Избранные рейсы с сервера:', data);

            // Создаем Set избранных flightId
            const favoriteIds = new Set();

            // Обрабатываем разные форматы ответа
            if (data.favorites && Array.isArray(data.favorites)) {
                data.favorites.forEach(f => {
                    if (f.flightId) favoriteIds.add(f.flightId);
                });
            } else if (Array.isArray(data)) {
                data.forEach(f => {
                    if (f.flightId) favoriteIds.add(f.flightId);
                });
            } else if (data.success && data.favorites) {
                data.favorites.forEach(f => {
                    if (f.flightId) favoriteIds.add(f.flightId);
                });
            }

            console.log('Избранные ID:', Array.from(favoriteIds));

            // Обновляем все кнопки на странице
            const buttons = document.querySelectorAll('.favorite-btn');
            buttons.forEach(button => {
                const flightId = button.getAttribute('data-flight-id');
                if (flightId) {
                    updateFavoriteButton(flightId, favoriteIds.has(flightId));
                }
            });

            console.log('Синхронизация завершена, обновлено кнопок:', buttons.length);
        } else {
            console.error('Ошибка при получении избранных рейсов:', response.status);
        }
    } catch (error) {
        console.error('Ошибка при синхронизации избранного:', error);
    }
}

// ==================== ГЛОБАЛЬНЫЕ ОБЪЯВЛЕНИЯ ====================
window.toggleFavorite = toggleFavorite;
window.selectRealFlight = selectRealFlight;
window.selectPopularDestination = selectPopularDestination;
window.checkAuthStatus = checkAuthStatus;
window.handleFavoriteClick = handleFavoriteClick;
window.showMyOrders = showMyOrders;
window.viewOrderDetails = viewOrderDetails;
window.cancelOrder = cancelOrder;

// ==================== ЗАГРУЗКА СТРАНИЦЫ ====================
document.addEventListener('DOMContentLoaded', function () {
    console.log('Инициализация страницы авиабилетов...');
    initializeFlightPage();
});