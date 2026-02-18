// wwwroot/js/friends.js
let friendsList = [];
let friendRequests = [];

// Загрузка списка друзей
async function loadFriends() {
    try {
        const response = await fetch('/api/friends/GetFriends');
        const result = await response.json();

        if (result.success) {
            friendsList = result.data || [];
            displayFriends(friendsList);
        }
    } catch (error) {
        console.error('Ошибка при загрузке друзей:', error);
    }
}

// Загрузка запросов в друзья
async function loadFriendRequests() {
    try {
        const response = await fetch('/api/friends/GetFriendRequests');
        const result = await response.json();

        if (result.success) {
            friendRequests = result.data || [];
            displayFriendRequests(friendRequests);
        }
    } catch (error) {
        console.error('Ошибка при загрузке запросов:', error);
    }
}

// Отображение друзей
function displayFriends(friends) {
    const container = document.getElementById('friendsList');
    if (!container) return;

    if (friends.length === 0) {
        container.innerHTML = '<div class="text-muted p-3">У вас пока нет друзей</div>';
        return;
    }

    let html = '';
    friends.forEach(friend => {
        html += `
            <div class="friend-item p-2 border-bottom d-flex justify-content-between align-items-center">
                <div class="d-flex align-items-center">
                    <div class="friend-avatar me-2">
                        ${friend.AvatarPath ?
                `<img src="${friend.AvatarPath}" class="rounded-circle" width="40" height="40">` :
                `<div class="avatar-placeholder rounded-circle bg-primary text-white d-flex align-items-center justify-content-center" style="width:40px;height:40px;">
                                ${friend.FirstName[0]}${friend.LastName[0]}
                            </div>`
            }
                    </div>
                    <div>
                        <div class="fw-bold">${friend.FullName}</div>
                        <small class="text-muted">${friend.Email}</small>
                    </div>
                </div>
                <div>
                    <button class="btn btn-sm btn-outline-primary" onclick="startPrivateChat(${friend.FriendId})">
                        <i class="fas fa-comment"></i>
                    </button>
                </div>
            </div>
        `;
    });

    container.innerHTML = html;
}

// Отображение запросов в друзья
function displayFriendRequests(requests) {
    const container = document.getElementById('friendRequests');
    if (!container) return;

    if (requests.length === 0) {
        container.innerHTML = '<div class="text-muted p-3">Нет новых запросов</div>';
        return;
    }

    let html = '';
    requests.forEach(request => {
        html += `
            <div class="request-item p-2 border-bottom" data-request-id="${request.Id}">
                <div class="d-flex justify-content-between align-items-center">
                    <div class="d-flex align-items-center">
                        <div class="me-2">
                            ${request.SenderAvatar ?
                `<img src="${request.SenderAvatar}" class="rounded-circle" width="40" height="40">` :
                `<div class="avatar-placeholder rounded-circle bg-primary text-white d-flex align-items-center justify-content-center" style="width:40px;height:40px;">?</div>`
            }
                        </div>
                        <div>
                            <div class="fw-bold">${request.SenderName}</div>
                            <small class="text-muted">${new Date(request.SentAt).toLocaleDateString()}</small>
                        </div>
                    </div>
                    <div>
                        <button class="btn btn-sm btn-success me-1" onclick="acceptFriendRequest(${request.Id})">
                            <i class="fas fa-check"></i>
                        </button>
                        <button class="btn btn-sm btn-danger" onclick="rejectFriendRequest(${request.Id})">
                            <i class="fas fa-times"></i>
                        </button>
                    </div>
                </div>
                ${request.Message ? `<div class="small text-muted mt-1">${request.Message}</div>` : ''}
            </div>
        `;
    });

    container.innerHTML = html;
}

// Принять запрос в друзья
async function acceptFriendRequest(requestId) {
    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        const response = await fetch('/api/friends/AcceptFriendRequest', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(requestId)
        });

        const result = await response.json();

        if (result.success) {
            showNotification('Друг добавлен!', 'success');
            loadFriendRequests();
            loadFriends();
        } else {
            showNotification(result.message || 'Ошибка', 'danger');
        }
    } catch (error) {
        console.error('Ошибка:', error);
        showNotification('Ошибка при принятии запроса', 'danger');
    }
}

// Отклонить запрос в друзья
async function rejectFriendRequest(requestId) {
    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        const response = await fetch('/api/friends/RejectFriendRequest', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(requestId)
        });

        const result = await response.json();

        if (result.success) {
            showNotification('Запрос отклонен', 'info');
            loadFriendRequests();
        } else {
            showNotification(result.message || 'Ошибка', 'danger');
        }
    } catch (error) {
        console.error('Ошибка:', error);
        showNotification('Ошибка при отклонении запроса', 'danger');
    }
}

// Поиск пользователей
async function searchUsers(term) {
    try {
        const response = await fetch(`/api/friends/SearchUsers?term=${encodeURIComponent(term)}`);
        const result = await response.json();

        if (result.success) {
            displaySearchResults(result.data || []);
        }
    } catch (error) {
        console.error('Ошибка при поиске:', error);
    }
}

// Отправка запроса в друзья
async function sendFriendRequest(userId) {
    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        const response = await fetch('/api/friends/SendFriendRequest', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(userId)
        });

        const result = await response.json();

        if (result.success) {
            showNotification('Запрос отправлен!', 'success');
            // Обновляем результаты поиска
            const searchInput = document.getElementById('searchUsersInput');
            if (searchInput && searchInput.value.length >= 2) {
                searchUsers(searchInput.value);
            }
        } else {
            showNotification(result.message || 'Ошибка', 'danger');
        }
    } catch (error) {
        console.error('Ошибка:', error);
        showNotification('Ошибка при отправке запроса', 'danger');
    }
}

// Отображение результатов поиска
function displaySearchResults(users) {
    const container = document.getElementById('searchResults');
    if (!container) return;

    if (users.length === 0) {
        container.innerHTML = '<div class="text-muted p-3">Ничего не найдено</div>';
        return;
    }

    let html = '';
    users.forEach(user => {
        let actionButton = '';

        if (user.IsFriend) {
            actionButton = '<button class="btn btn-sm btn-success" disabled><i class="fas fa-check"></i> Друг</button>';
        } else if (user.FriendStatus === 'pending_sent') {
            actionButton = '<button class="btn btn-sm btn-secondary" disabled><i class="fas fa-clock"></i> Запрос отправлен</button>';
        } else if (user.FriendStatus === 'pending_received') {
            actionButton = '<button class="btn btn-sm btn-warning" disabled><i class="fas fa-hourglass"></i> Ожидает ответа</button>';
        } else {
            actionButton = `<button class="btn btn-sm btn-primary" onclick="sendFriendRequest(${user.Id})">
                <i class="fas fa-user-plus"></i> Добавить в друзья
            </button>`;
        }

        html += `
            <div class="search-result-item p-2 border-bottom d-flex justify-content-between align-items-center">
                <div class="d-flex align-items-center">
                    <div class="me-2">
                        ${user.AvatarPath ?
                `<img src="${user.AvatarPath}" class="rounded-circle" width="40" height="40">` :
                `<div class="avatar-placeholder rounded-circle bg-primary text-white d-flex align-items-center justify-content-center" style="width:40px;height:40px;">
                                ${user.FirstName[0]}${user.LastName[0]}
                            </div>`
            }
                    </div>
                    <div>
                        <div class="fw-bold">${user.FullName}</div>
                        <small class="text-muted">${user.Email}</small>
                    </div>
                </div>
                <div>
                    ${actionButton}
                </div>
            </div>
        `;
    });

    container.innerHTML = html;
}

// Инициализация при загрузке страницы
document.addEventListener('DOMContentLoaded', function () {
    // Загружаем друзей и запросы
    loadFriends();
    loadFriendRequests();

    // Обработчик поиска
    const searchInput = document.getElementById('searchUsersInput');
    if (searchInput) {
        let searchTimeout;
        searchInput.addEventListener('input', function () {
            clearTimeout(searchTimeout);
            const term = this.value.trim();
            if (term.length >= 2) {
                searchTimeout = setTimeout(() => searchUsers(term), 300);
            } else {
                document.getElementById('searchResults').innerHTML = '';
            }
        });
    }
});