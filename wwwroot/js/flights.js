// Глобальные переменные
let currentDropdown = null;
let currentInput = null;
let timeoutId;
let isUserAuthenticated = false;
let userId = null;

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
async function toggleFavorite(flightData) {
    if (!isUserAuthenticated) {
        showAuthRequiredModal();
        return;
    }

    try {
        const flightId = flightData.flightId;
        const checkResponse = await fetch(`/api/favorites/flights/check/${encodeURIComponent(flightId)}`, {
            credentials: 'include'
        });

        if (!checkResponse.ok) {
            const errorText = await checkResponse.text();
            throw new Error(`Ошибка проверки: ${errorText}`);
        }

        const checkData = await checkResponse.json();
        console.log('Результат проверки избранного:', checkData);

        if (checkData.isFavorite) {
            const deleteResponse = await fetch(`/api/favorites/flights/${encodeURIComponent(flightId)}`, {
                method: 'DELETE',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' }
            });

            if (deleteResponse.ok) {
                updateFavoriteButton(flightId, false);
                showNotification('Рейс удален из избранного', 'info');
            } else {
                const errorData = await deleteResponse.json();
                throw new Error(errorData.message || 'Ошибка при удалении');
            }
        } else {
            const addResponse = await fetch('/api/favorites/flights', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                credentials: 'include',
                body: JSON.stringify(flightData)
            });

            const result = await addResponse.json();
            console.log('Результат добавления в избранное:', result);

            if (result.success) {
                updateFavoriteButton(flightId, true);
                showNotification('Рейс добавлен в избранное!', 'success');
            } else {
                showNotification(result.message || 'Ошибка при сохранении', 'danger');
            }
        }
    } catch (error) {
        console.error('Ошибка при работе с избранным:', error);
        showNotification(error.message || 'Ошибка при сохранении рейса', 'danger');
    }
}

function updateFavoriteButton(flightId, isFavorite) {
    const buttons = document.querySelectorAll(`[data-flight-id="${CSS.escape(flightId)}"]`);
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
function showFlightResults(flights, searchData) {
    console.log('=== ПОКАЗ РЕЗУЛЬТАТОВ ===');
    console.log('Получено рейсов:', flights?.length || 0);
    console.log('Пользователь авторизован:', isUserAuthenticated);

    const oldResults = document.getElementById('flightResultsContainer');
    if (oldResults) oldResults.innerHTML = '';

    if (!flights || flights.length === 0) {
        document.getElementById('flightResultsContainer').innerHTML = `
            <div class="alert alert-info mt-4">
                <h5 class="alert-heading">Рейсы не найдены</h5>
                <p>Попробуйте изменить параметры поиска или даты</p>
            </div>
        `;
        return;
    }

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

    document.getElementById('flightResultsContainer').innerHTML = html;

    // Назначаем обработчики для новых кнопок
    const favoriteButtons = document.querySelectorAll('.favorite-btn');
    favoriteButtons.forEach(button => {
        button.addEventListener('click', function () {
            handleFavoriteClick(this);
        });
    });

    requestAnimationFrame(() => {
        if (isUserAuthenticated) checkFavoritesForFlights();
    });

    setTimeout(() => {
        const resultsElement = document.getElementById('flightResultsContainer');
        if (resultsElement) {
            resultsElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }, 200);
}

function buildStableFlightId(flight, isReturnFlight) {
    return [
        flight.airlineCode || flight.airline || 'AIR',
        flight.flightNumber || 'FN',
        flight.departureCity || 'FROM',
        flight.arrivalCity || 'TO',
        formatDateForApi(flight.departureTime),
        isReturnFlight ? 'R' : 'O'
    ].join('_');
}

function generateFlightCard(flight, index, isReturnFlight) {
    if (!flight) return '';

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
    const flightId = button.getAttribute('data-flight-id');
    const flightDataStr = button.getAttribute('data-flight-data');

    if (!flightDataStr) {
        console.error('Данные рейса не найдены');
        return;
    }

    try {
        const flightData = JSON.parse(flightDataStr.replace(/&apos;/g, "'"));
        await toggleFavorite(flightData);
    } catch (error) {
        console.error('Ошибка при обработке данных рейса:', error);
        showNotification('Ошибка при сохранении рейса', 'danger');
    }
}

async function checkFavoritesForFlights() {
    if (!isUserAuthenticated) return;

    const buttons = document.querySelectorAll('.favorite-btn');
    console.log('Проверяем избранное для', buttons.length, 'кнопок');

    for (const button of buttons) {
        const flightId = button.getAttribute('data-flight-id');
        if (!flightId) continue;

        try {
            const response = await fetch(`/api/favorites/flights/check/${encodeURIComponent(flightId)}`, {
                credentials: 'include'
            });

            if (response.ok) {
                const data = await response.json();
                if (data.isFavorite) {
                    updateFavoriteButton(flightId, true);
                }
            } else {
                console.error('Ошибка ответа при проверке избранного:', response.status);
            }
        } catch (error) {
            console.error('Ошибка проверки избранного для рейса:', flightId, error);
        }
    }
}

// ==================== ПОКУПКА БИЛЕТОВ ====================
function selectRealFlight(flightId, price, airline, isReturn) {
    const flightCard = document.querySelector(`[data-flight-id="${CSS.escape(flightId)}"]`)?.closest('.flight-card');

    if (!flightCard) {
        alert('Не удалось получить данные рейса');
        return;
    }

    const departureCity = flightCard.querySelectorAll('.text-muted')[1]?.textContent.trim() || 'Москва';
    const arrivalCity = flightCard.querySelectorAll('.text-muted')[3]?.textContent.trim() || 'Санкт-Петербург';
    const departureTime = flightCard.querySelector('.time-display')?.textContent.trim() || '08:00';
    const arrivalTime = flightCard.querySelectorAll('.time-display')[1]?.textContent.trim() || '10:00';

    const flightData = {
        flightId: flightId,
        airline: airline,
        flightNumber: `${airline} ${Math.floor(Math.random() * 9000) + 1000}`,
        departureCity: departureCity,
        arrivalCity: arrivalCity,
        departureAirport: departureCity.includes('Москва') ? 'SVO' : 'LED',
        arrivalAirport: arrivalCity.includes('Москва') ? 'SVO' : 'LED',
        departureTime: new Date(new Date().setHours(parseInt(departureTime.split(':')[0]), parseInt(departureTime.split(':')[1]))),
        arrivalTime: new Date(new Date().setHours(parseInt(arrivalTime.split(':')[0]), parseInt(arrivalTime.split(':')[1]))),
        price: price,
        currency: 'RUB',
        transfers: 0,
        isReturn: isReturn
    };

    showBookingModal(flightData, flightId);
}

function showBookingModal(flightData, flightId) {
    console.log('Показ модального окна покупки для рейса:', flightId);

    // Убедитесь, что flightData содержит все необходимые поля
    const completeFlightData = {
        ...flightData,
        flightId: flightId,
        // Добавляем недостающие поля
        airline: flightData.airline || 'Аэрофлот',
        flightNumber: flightData.flightNumber || 'SU 1234',
        departureCity: flightData.departureCity || 'Москва',
        arrivalCity: flightData.arrivalCity || 'Санкт-Петербург',
        departureAirport: flightData.departureAirport || 'SVO',
        arrivalAirport: flightData.arrivalAirport || 'LED',
        departureTime: flightData.departureTime || new Date(),
        arrivalTime: flightData.arrivalTime || new Date(),
        price: flightData.price || 5000,
        currency: flightData.currency || 'RUB',
        transfers: flightData.transfers || 0,
        duration: flightData.duration || 120,
        isReturn: flightData.isReturn || false
    };

    const modalHtml = `
        <div class="modal fade" id="bookingModal" tabindex="-1" aria-hidden="true">
            <div class="modal-dialog modal-lg modal-dialog-centered">
                <div class="modal-content">
                    <div class="modal-header bg-primary text-white">
                        <h5 class="modal-title">
                            <i class="fas fa-plane me-2"></i>
                            Бронирование рейса
                        </h5>
                        <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body p-4">
                        <div class="alert alert-info mb-4">
                            <div class="d-flex align-items-center">
                                <i class="fas fa-info-circle fa-2x me-3"></i>
                                <div>
                                    <h6 class="mb-1">Демо-режим</h6>
                                    <p class="mb-0 small">Это демонстрационная покупка. Данные не сохраняются в реальной системе.</p>
                                </div>
                            </div>
                        </div>

                        <div class="card mb-4">
                            <div class="card-header bg-light">
                                <h6 class="mb-0">Информация о рейсе</h6>
                            </div>
                            <div class="card-body">
                                <div class="row">
                                    <div class="col-md-6">
                                        <p><strong>Авиакомпания:</strong> ${flightData.airline || 'Аэрофлот'}</p>
                                        <p><strong>Рейс:</strong> ${flightData.flightNumber || 'SU 1234'}</p>
                                        <p><strong>Маршрут:</strong> ${flightData.departureCity} → ${flightData.arrivalCity}</p>
                                    </div>
                                    <div class="col-md-6">
                                        <p><strong>Вылет:</strong> ${formatTime(flightData.departureTime)}</p>
                                        <p><strong>Прилет:</strong> ${formatTime(flightData.arrivalTime)}</p>
                                        <p><strong>Цена:</strong> <span class="text-primary fw-bold">${flightData.price ? flightData.price.toLocaleString('ru-RU') : '0'} RUB</span></p>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <form id="bookingForm">
                            <div class="mb-4">
                                <h6 class="mb-3"><i class="fas fa-users me-2"></i>Данные пассажиров</h6>
                                <div id="passengersContainer">
                                    <!-- Пассажиры будут добавляться здесь -->
                                </div>
                                <button type="button" class="btn btn-outline-primary btn-sm mt-2" onclick="addPassengerField()">
                                    <i class="fas fa-plus me-1"></i>Добавить пассажира
                                </button>
                            </div>

                            <div class="mb-4">
                                <h6 class="mb-3"><i class="fas fa-address-card me-2"></i>Контактная информация</h6>
                                <div class="row g-3">
                                    <div class="col-md-6">
                                        <label class="form-label">Email *</label>
                                        <input type="email" class="form-control" name="contactEmail" required>
                                    </div>
                                    <div class="col-md-6">
                                        <label class="form-label">Телефон *</label>
                                        <input type="tel" class="form-control" name="contactPhone" required>
                                    </div>
                                </div>
                            </div>

                            <div class="mb-4">
                                <h6 class="mb-3"><i class="fas fa-credit-card me-2"></i>Оплата (демо)</h6>
                                <div class="alert alert-warning">
                                    <i class="fas fa-exclamation-triangle me-2"></i>
                                    Демо-данные платежа. Никакие реальные платежи не осуществляются.
                                </div>
                                <div class="row g-3">
                                    <div class="col-md-6">
                                        <label class="form-label">Номер карты</label>
                                        <input type="text" class="form-control" value="4242 4242 4242 4242" readonly>
                                    </div>
                                    <div class="col-md-3">
                                        <label class="form-label">Срок действия</label>
                                        <input type="text" class="form-control" value="12/28" readonly>
                                    </div>
                                    <div class="col-md-3">
                                        <label class="form-label">CVV</label>
                                        <input type="text" class="form-control" value="123" readonly>
                                    </div>
                                    <div class="col-md-12">
                                        <label class="form-label">Имя держателя карты</label>
                                        <input type="text" class="form-control" value="DEMO USER" readonly>
                                    </div>
                                </div>
                            </div>

                            <div class="d-grid gap-2">
                                <button type="submit" class="btn btn-success btn-lg">
                                    <i class="fas fa-check me-2"></i>Подтвердить бронирование
                                </button>
                                <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">
                                    <i class="fas fa-times me-2"></i>Отмена
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            </div>
        </div>
    `;

    document.body.insertAdjacentHTML('beforeend', modalHtml);
    addPassengerField();

    const bookingModal = new bootstrap.Modal(document.getElementById('bookingModal'));
    bookingModal.show();

    document.getElementById('bookingForm').addEventListener('submit', async function (e) {
        e.preventDefault();
        await processBooking(completeFlightData, flightId, this);
    });

    document.getElementById('bookingModal').addEventListener('hidden.bs.modal', function () {
        this.remove();
    });
}

function addPassengerField() {
    const container = document.getElementById('passengersContainer');
    const passengerIndex = container.children.length + 1;

    const passengerHtml = `
        <div class="card mb-3 passenger-card">
            <div class="card-header bg-light d-flex justify-content-between align-items-center">
                <h6 class="mb-0">Пассажир ${passengerIndex}</h6>
                ${passengerIndex > 1 ? '<button type="button" class="btn btn-sm btn-outline-danger" onclick="removePassenger(this)"><i class="fas fa-times"></i></button>' : ''}
            </div>
            <div class="card-body">
                <div class="row g-3">
                    <div class="col-md-4">
                        <label class="form-label">Имя *</label>
                        <input type="text" class="form-control passenger-firstname" placeholder="Иван" required>
                    </div>
                    <div class="col-md-4">
                        <label class="form-label">Фамилия *</label>
                        <input type="text" class="form-control passenger-lastname" placeholder="Иванов" required>
                    </div>
                    <div class="col-md-4">
                        <label class="form-label">Отчество</label>
                        <input type="text" class="form-control passenger-middlename" placeholder="Иванович">
                    </div>
                    <div class="col-md-4">
                        <label class="form-label">Дата рождения *</label>
                        <input type="date" class="form-control passenger-birthdate" required>
                    </div>
                    <div class="col-md-4">
                        <label class="form-label">Пол *</label>
                        <select class="form-select passenger-gender" required>
                            <option value="M">Мужской</option>
                            <option value="F">Женский</option>
                        </select>
                    </div>
                    <div class="col-md-4">
                        <label class="form-label">Гражданство *</label>
                        <input type="text" class="form-control passenger-nationality" value="Россия" required>
                    </div>
                    <div class="col-md-6">
                        <label class="form-label">Тип документа *</label>
                        <select class="form-select passenger-doctype" required>
                            <option value="passport">Паспорт</option>
                            <option value="internal_passport">Внутренний паспорт</option>
                            <option value="birth_certificate">Свидетельство о рождении</option>
                        </select>
                    </div>
                    <div class="col-md-6">
                        <label class="form-label">Номер документа *</label>
                        <input type="text" class="form-control passenger-docnumber" placeholder="1234 567890" required>
                    </div>
                </div>
            </div>
        </div>
    `;

    container.insertAdjacentHTML('beforeend', passengerHtml);
}

function removePassenger(button) {
    button.closest('.passenger-card').remove();
    document.querySelectorAll('.passenger-card .card-header h6').forEach((header, index) => {
        header.textContent = `Пассажир ${index + 1}`;
    });
}

async function processBooking(flightData, flightId, formElement) {
    try {
        const passengers = [];
        document.querySelectorAll('.passenger-card').forEach(card => {
            passengers.push({
                firstName: card.querySelector('.passenger-firstname').value,
                lastName: card.querySelector('.passenger-lastname').value,
                middleName: card.querySelector('.passenger-middlename').value || '',
                dateOfBirth: card.querySelector('.passenger-birthdate').value,
                gender: card.querySelector('.passenger-gender').value,
                nationality: card.querySelector('.passenger-nationality').value,
                documentType: card.querySelector('.passenger-doctype').value,
                documentNumber: card.querySelector('.passenger-docnumber').value
            });
        });

        const contact = {
            email: formElement.querySelector('[name="contactEmail"]').value,
            phone: formElement.querySelector('[name="contactPhone"]').value
        };

        const payment = {
            method: "card",
            cardNumber: "4242424242424242",
            cardHolder: "DEMO USER",
            expiryMonth: "12",
            expiryYear: "28",
            cvv: "123"
        };

        // Создаем корректный объект для отправки
        const bookingRequest = {
            flightId: flightId,
            flightData: flightData, // Добавляем полные данные о рейсе
            passengers: passengers,
            contact: contact,
            payment: payment,
            // Убираем лишние поля которые могут конфликтовать
            selectedFlight: null, // Убираем это поле
            searchId: null // Убираем это поле
        };

        console.log('Отправка запроса на бронирование:', bookingRequest);

        const submitBtn = formElement.querySelector('button[type="submit"]');
        const originalText = submitBtn.innerHTML;
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Обработка...';
        submitBtn.disabled = true;

        try {
            const response = await fetch('/api/flights/book', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                credentials: 'include', // Важно для передачи кук авторизации
                body: JSON.stringify(bookingRequest)
            });

            console.log('Статус ответа бронирования:', response.status);

            if (!response.ok) {
                let errorMessage = `HTTP error! status: ${response.status}`;
                try {
                    const errorData = await response.json();
                    errorMessage = errorData.message || errorData.error || errorMessage;
                } catch (e) {
                    // Если не удалось распарсить JSON
                }
                throw new Error(errorMessage);
            }

            const result = await response.json();
            console.log('Ответ от сервера:', result);

            if (result.success) {
                // Закрываем модальное окно
                const bookingModal = document.getElementById('bookingModal');
                if (bookingModal) {
                    const modal = bootstrap.Modal.getInstance(bookingModal);
                    if (modal) modal.hide();
                }

                // Показываем уведомление об успехе
                showSuccessNotification(result);
            } else {
                alert(result.message || 'Ошибка при бронировании');
                submitBtn.innerHTML = originalText;
                submitBtn.disabled = false;
            }
        } catch (error) {
            console.error('Ошибка при бронировании:', error);

            // Проверяем, если ошибка авторизации
            if (error.message.includes('401') || error.message.includes('Unauthorized')) {
                showAuthRequiredModal();
                return;
            }

            alert('Ошибка при бронировании: ' + error.message);
            submitBtn.innerHTML = originalText;
            submitBtn.disabled = false;
        }
    } catch (error) {
        console.error('Общая ошибка при бронировании:', error);
        alert('Произошла ошибка: ' + error.message);
    }
}

function showSuccessNotification(result) {
    const notificationHtml = `
        <div class="modal fade" id="successModal" tabindex="-1">
            <div class="modal-dialog modal-dialog-centered">
                <div class="modal-content">
                    <div class="modal-header bg-success text-white">
                        <h5 class="modal-title">
                            <i class="fas fa-check-circle me-2"></i>
                            Бронирование успешно!
                        </h5>
                    </div>
                    <div class="modal-body text-center py-4">
                        <i class="fas fa-plane-circle-check text-success mb-3" style="font-size: 4rem;"></i>
                        <h4 class="mb-3">Ваш билет забронирован!</h4>

                        <div class="card mb-3">
                            <div class="card-body">
                                <p class="mb-1"><strong>Номер заказа:</strong> ${result.orderNumber}</p>
                                <p class="mb-1"><strong>Номер билета:</strong> ${result.ticketNumber}</p>
                                <p class="mb-1"><strong>Сумма:</strong> ${result.totalPrice ? result.totalPrice.toLocaleString('ru-RU') : '0'} RUB</p>
                                <p class="mb-0"><strong>Статус:</strong> <span class="badge bg-success">${result.status || 'подтвержден'}</span></p>
                            </div>
                        </div>

                        <div class="alert alert-info text-start">
                            <h6 class="alert-heading"><i class="fas fa-info-circle me-2"></i>Демо-режим</h6>
                            <p class="mb-0 small">
                                Это демонстрационная покупка. В реальной системе:
                                <ul class="small mb-0">
                                    <li>Билет был бы отправлен на вашу почту</li>
                                    <li>Произведен реальный платеж</li>
                                    <li>Были бы сгенерированы реальные посадочные талоны</li>
                                </ul>
                            </p>
                        </div>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-outline-secondary" onclick="printDemoTicket('${result.ticketNumber}')">
                            <i class="fas fa-print me-2"></i>Распечатать демо-билет
                        </button>
                        <button type="button" class="btn btn-primary" onclick="closeSuccessModal()">
                            <i class="fas fa-check me-2"></i>Отлично!
                        </button>
                    </div>
                </div>
            </div>
        </div>
    `;

    document.body.insertAdjacentHTML('beforeend', notificationHtml);

    const successModal = new bootstrap.Modal(document.getElementById('successModal'), {
        backdrop: 'static',
        keyboard: false
    });
    successModal.show();
}

function printDemoTicket(ticketNumber) {
    const ticketHtml = `
        <!DOCTYPE html>
        <html>
        <head>
            <title>Демо-билет ${ticketNumber}</title>
            <style>
                body { font-family: Arial, sans-serif; padding: 20px; }
                .ticket { border: 2px solid #000; padding: 20px; max-width: 600px; margin: 0 auto; }
                .header { text-align: center; margin-bottom: 30px; }
                .airline-logo { font-size: 24px; font-weight: bold; color: #007bff; }
                .flight-info { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 30px; }
                .info-box { border: 1px solid #ddd; padding: 10px; }
                .barcode { text-align: center; margin: 20px 0; font-family: monospace; }
                .footer { text-align: center; margin-top: 30px; font-size: 12px; color: #666; }
                @media print { 
                    body { padding: 0; }
                    .no-print { display: none; }
                }
            </style>
        </head>
        <body>
            <div class="ticket">
                <div class="header">
                    <div class="airline-logo">✈ DEMO AIRLINES</div>
                    <h2>ПОСАДОЧНЫЙ ТАЛОН</h2>
                    <p><strong>Номер билета:</strong> ${ticketNumber}</p>
                </div>
                
                <div class="flight-info">
                    <div class="info-box">
                        <strong>Рейс:</strong> DEMO-123<br>
                        <strong>Класс:</strong> Эконом<br>
                        <strong>Дата:</strong> ${new Date().toLocaleDateString('ru-RU')}
                    </div>
                    <div class="info-box">
                        <strong>Вылет:</strong> 08:00 (SVO)<br>
                        <strong>Прилет:</strong> 10:00 (LED)<br>
                        <strong>Время в пути:</strong> 2ч 00м
                    </div>
                </div>
                
                <div class="info-box">
                    <strong>Пассажир:</strong> Демо Пользователь<br>
                    <strong>Место:</strong> 12A<br>
                    <strong>Выход на посадку:</strong> A1<br>
                    <strong>Начало посадки:</strong> 07:00
                </div>
                
                <div class="barcode">
                    <div>*** ДЕМО-БИЛЕТ ***</div>
                    <div style="letter-spacing: 3px; font-size: 18px;">||| || ||| || ||| || ||| ||</div>
                    <div>${ticketNumber}</div>
                </div>
                
                <div class="footer">
                    <p>Это демонстрационный билет. Не действителен для реальной посадки.</p>
                    <p>Распечатано: ${new Date().toLocaleString('ru-RU')}</p>
                </div>
                
                <div class="no-print" style="text-align: center; margin-top: 20px;">
                    <button onclick="window.print()" style="padding: 10px 20px; background: #007bff; color: white; border: none; cursor: pointer;">
                        🖨 Распечатать билет
                    </button>
                    <button onclick="window.close()" style="padding: 10px 20px; background: #666; color: white; border: none; cursor: pointer; margin-left: 10px;">
                        ✕ Закрыть
                    </button>
                </div>
            </div>
        </body>
        </html>
    `;

    const printWindow = window.open('', '_blank');
    printWindow.document.write(ticketHtml);
    printWindow.document.close();
}

function closeSuccessModal() {
    const modal = document.getElementById('successModal');
    if (modal) {
        bootstrap.Modal.getInstance(modal).hide();
        modal.remove();
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
function initializeFlightPage() {
    const departureInput = document.getElementById('departureCity');
    const arrivalInput = document.getElementById('arrivalCity');
    const departureDropdown = document.getElementById('departureDropdown');
    const arrivalDropdown = document.getElementById('arrivalDropdown');

    // Проверяем авторизацию
    checkAuthStatus();

    // Обработчики автозаполнения
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
    }

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
    }

    // Закрытие dropdown при клике вне
    document.addEventListener('click', (e) => {
        if (!e.target.closest('.city-autocomplete')) {
            if (departureDropdown) departureDropdown.style.display = 'none';
            if (arrivalDropdown) arrivalDropdown.style.display = 'none';
        }
    });

    // Навигация с клавиатуры
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
                if (activeItem) activeItem.click();
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

            if (!departureCity || !arrivalCity) {
                showNotification('Пожалуйста, заполните города вылета и прилета', 'warning');
                return;
            }

            if (!departureDate) {
                showNotification('Пожалуйста, выберите дату вылета', 'warning');
                return;
            }

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
                console.error('Ошибка:', error);
                showNotification(`Ошибка: ${error.message}`, 'danger');
            } finally {
                submitBtn.innerHTML = originalText;
                submitBtn.disabled = false;
            }
        });

        // Установка дат по умолчанию
        const today = new Date().toISOString().split('T')[0];
        const tomorrow = new Date();
        tomorrow.setDate(tomorrow.getDate() + 1);
        const tomorrowStr = tomorrow.toISOString().split('T')[0];

        const departureDateInput = document.getElementById('departureDate');
        const returnDateInput = document.getElementById('returnDate');

        if (departureDateInput) {
            departureDateInput.min = today;
            departureDateInput.value = tomorrowStr;
        }

        if (returnDateInput) {
            returnDateInput.min = tomorrowStr;
        }

        if (departureDateInput && returnDateInput) {
            departureDateInput.addEventListener('change', function () {
                returnDateInput.min = this.value;
            });
        }
    }

    // Добавляем кнопку "Мои заказы"
    const headerControls = document.querySelector('.container .row .col-12');
    if (headerControls) {
        const ordersBtn = document.createElement('button');
        ordersBtn.className = 'btn btn-outline-primary mb-4 me-2';
        ordersBtn.innerHTML = '<i class="fas fa-ticket-alt me-2"></i>Мои заказы';
        ordersBtn.onclick = showMyOrders;
        headerControls.insertBefore(ordersBtn, headerControls.firstChild);
    }

    // Загружаем популярные направления
    loadPopularDestinations();
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
window.printDemoTicket = printDemoTicket;
window.addPassengerField = addPassengerField;
window.removePassenger = removePassenger;

// ==================== ЗАГРУЗКА СТРАНИЦЫ ====================
document.addEventListener('DOMContentLoaded', function () {
    console.log('Инициализация страницы авиабилетов...');
    initializeFlightPage();
});