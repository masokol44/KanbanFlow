// kanban_js.js — Менеджер задач (канбан) на JavaScript (Node.js + readline)

const fs = require('fs');
const readline = require('readline');

const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
    prompt: '> '
});

let tasks = [];
let nextId = 1;
const columns = ["To Do", "In Progress", "Done"];
const dataFile = "kanban.json";

function loadData() {
    try {
        if (fs.existsSync(dataFile)) {
            const data = fs.readFileSync(dataFile, 'utf8');
            tasks = JSON.parse(data);
            tasks.forEach(t => { if (t.id >= nextId) nextId = t.id + 1; });
        }
    } catch (e) {}
}

function saveData() {
    fs.writeFileSync(dataFile, JSON.stringify(tasks, null, 2));
}

function askQuestion(query) {
    return new Promise((resolve) => {
        rl.question(query, resolve);
    });
}

async function addTask() {
    const title = (await askQuestion('Заголовок: ')).trim();
    if (!title) { console.log('Заголовок обязателен'); return; }
    const description = (await askQuestion('Описание: ')).trim();
    let priority = (await askQuestion('Приоритет (high/medium/low): ')).trim() || 'medium';
    if (!['high','medium','low'].includes(priority)) priority = 'medium';
    const deadline = (await askQuestion('Дедлайн (ГГГГ-ММ-ДД): ')).trim();
    const tags = (await askQuestion('Теги (через запятую): ')).trim();
    const now = new Date().toISOString();
    const task = {
        id: nextId++,
        title,
        description,
        priority,
        deadline,
        tags,
        column: "To Do",
        created: now,
        updated: now,
        completed: ""
    };
    tasks.push(task);
    saveData();
    console.log(`Задача #${task.id} добавлена в колонку 'To Do'`);
}

function listTasks() {
    for (const col of columns) {
        console.log(`\n=== ${col} ===`);
        const found = tasks.filter(t => t.column === col);
        if (found.length === 0) {
            console.log('(пусто)');
        } else {
            found.forEach(t => {
                const priorityColor = t.priority === 'high' ? '🔴' : (t.priority === 'medium' ? '🟡' : '🟢');
                let line = `#${t.id} ${priorityColor} ${t.title}`;
                if (t.deadline) line += ` (до ${t.deadline})`;
                if (t.tags) line += ` [${t.tags}]`;
                console.log(line);
            });
        }
    }
}

async function moveTask() {
    const idStr = await askQuestion('Номер задачи: ');
    const id = parseInt(idStr);
    const task = tasks.find(t => t.id === id);
    if (!task) { console.log('Задача не найдена'); return; }
    console.log(`Текущая колонка: ${task.column}`);
    const col = await askQuestion('В какую колонку переместить? ');
    if (!col) { console.log('Колонка не указана'); return; }
    if (!columns.includes(col)) { console.log('Неверная колонка. Допустимые:', columns.join(', ')); return; }
    task.column = col;
    task.updated = new Date().toISOString();
    if (col === 'Done') task.completed = task.updated;
    saveData();
    console.log(`Задача #${task.id} перемещена в '${col}'`);
}

async function editTask() {
    const idStr = await askQuestion('Номер задачи: ');
    const id = parseInt(idStr);
    const task = tasks.find(t => t.id === id);
    if (!task) { console.log('Задача не найдена'); return; }
    const title = (await askQuestion(`Заголовок (${task.title}): `)).trim();
    if (title) task.title = title;
    const description = (await askQuestion(`Описание (${task.description}): `)).trim();
    if (description) task.description = description;
    const priority = (await askQuestion(`Приоритет (${task.priority}): `)).trim();
    if (priority) task.priority = priority;
    const deadline = (await askQuestion(`Дедлайн (${task.deadline}): `)).trim();
    if (deadline) task.deadline = deadline;
    const tags = (await askQuestion(`Теги (${task.tags}): `)).trim();
    if (tags) task.tags = tags;
    task.updated = new Date().toISOString();
    saveData();
    console.log(`Задача #${task.id} обновлена`);
}

async function deleteTask() {
    const idStr = await askQuestion('Номер задачи: ');
    const id = parseInt(idStr);
    const task = tasks.find(t => t.id === id);
    if (!task) { console.log('Задача не найдена'); return; }
    const ans = await askQuestion(`Удалить задачу #${id}? (y/n): `);
    if (ans.toLowerCase() === 'y') {
        tasks = tasks.filter(t => t.id !== id);
        saveData();
        console.log('Удалено');
    } else {
        console.log('Отменено');
    }
}

async function searchTasks() {
    const query = (await askQuestion('Введите текст для поиска: ')).trim().toLowerCase();
    if (!query) { console.log('Текст не указан'); return; }
    const found = tasks.filter(t => t.title.toLowerCase().includes(query) || t.description.toLowerCase().includes(query));
    if (found.length === 0) {
        console.log('Ничего не найдено');
        return;
    }
    console.log(`Найдено ${found.length} задач:`);
    found.forEach(t => console.log(`#${t.id} [${t.priority}] ${t.title} (колонка: ${t.column})`));
}

function showStats() {
    const total = tasks.length;
    const colCounts = {};
    columns.forEach(c => colCounts[c] = 0);
    let done = 0, overdue = 0;
    const today = new Date().toISOString().slice(0,10);
    tasks.forEach(t => {
        colCounts[t.column] = (colCounts[t.column] || 0) + 1;
        if (t.column === 'Done') done++;
        if (t.deadline && t.deadline < today && t.column !== 'Done') overdue++;
    });
    console.log(`Всего задач: ${total}`);
    columns.forEach(c => console.log(`${c}: ${colCounts[c] || 0}`));
    console.log(`Выполнено: ${done}`);
    console.log(`Просрочено: ${overdue}`);
}

async function exportData() {
    const fname = (await askQuestion('Имя файла для экспорта (JSON): ')).trim() || 'export.json';
    fs.writeFileSync(fname, JSON.stringify(tasks, null, 2));
    console.log(`Экспортировано в ${fname}`);
}

async function importData() {
    const fname = (await askQuestion('Имя файла для импорта (JSON): ')).trim();
    if (!fname) { console.log('Имя не указано'); return; }
    try {
        const data = fs.readFileSync(fname, 'utf8');
        const imported = JSON.parse(data);
        imported.forEach(t => {
            if (t.id >= nextId) nextId = t.id + 1;
            tasks.push(t);
        });
        saveData();
        console.log(`Импортировано ${imported.length} задач`);
    } catch (e) {
        console.log('Ошибка импорта:', e.message);
    }
}

function exit() {
    saveData();
    console.log('До свидания!');
    rl.close();
}

loadData();
console.log('📋 KanbanFlow — JavaScript Edition');
console.log('Колонки:', columns.join(', '));
console.log('Команды: add, list, move, edit, delete, search, stats, export, import, exit');
rl.prompt();

rl.on('line', async (line) => {
    const cmd = line.trim();
    switch (cmd) {
        case 'add': await addTask(); break;
        case 'list': listTasks(); break;
        case 'move': await moveTask(); break;
        case 'edit': await editTask(); break;
        case 'delete': await deleteTask(); break;
        case 'search': await searchTasks(); break;
        case 'stats': showStats(); break;
        case 'export': await exportData(); break;
        case 'import': await importData(); break;
        case 'exit': exit(); break;
        default: console.log('Неизвестная команда');
    }
    rl.prompt();
}).on('close', () => {
    saveData();
    process.exit(0);
});
