// kanban_rs.rs — Менеджер задач (канбан) на Rust (консоль + termion)

use serde::{Deserialize, Serialize};
use std::fs;
use std::io::{self, Write, BufRead};
use std::str::FromStr;
use std::time::{SystemTime, UNIX_EPOCH};
use termion::{color, style};

#[derive(Serialize, Deserialize, Clone)]
struct Task {
    id: i32,
    title: String,
    description: String,
    priority: String,
    deadline: String,
    tags: String,
    column: String,
    created: String,
    updated: String,
    completed: String,
}

struct App {
    tasks: Vec<Task>,
    next_id: i32,
    columns: Vec<String>,
    filename: String,
}

impl App {
    fn new() -> Self {
        let mut app = App {
            tasks: Vec::new(),
            next_id: 1,
            columns: vec!["To Do".to_string(), "In Progress".to_string(), "Done".to_string()],
            filename: "kanban.json".to_string(),
        };
        app.load();
        app
    }

    fn load(&mut self) {
        if let Ok(data) = fs::read_to_string(&self.filename) {
            if let Ok(tasks) = serde_json::from_str::<Vec<Task>>(&data) {
                self.tasks = tasks;
                for t in &self.tasks {
                    if t.id >= self.next_id {
                        self.next_id = t.id + 1;
                    }
                }
            }
        }
    }

    fn save(&self) {
        let data = serde_json::to_string_pretty(&self.tasks).unwrap();
        fs::write(&self.filename, data).unwrap();
    }

    fn add_task(&mut self) {
        let reader = io::stdin();
        let mut reader = reader.lock();
        print!("Заголовок: ");
        io::stdout().flush().unwrap();
        let mut title = String::new();
        reader.read_line(&mut title).unwrap();
        let title = title.trim();
        if title.is_empty() { println!("Заголовок обязателен"); return; }
        print!("Описание: ");
        io::stdout().flush().unwrap();
        let mut desc = String::new();
        reader.read_line(&mut desc).unwrap();
        let desc = desc.trim();
        print!("Приоритет (high/medium/low): ");
        io::stdout().flush().unwrap();
        let mut priority = String::new();
        reader.read_line(&mut priority).unwrap();
        let priority = priority.trim();
        let priority = if priority.is_empty() { "medium".to_string() } else { priority.to_string() };
        print!("Дедлайн (ГГГГ-ММ-ДД): ");
        io::stdout().flush().unwrap();
        let mut deadline = String::new();
        reader.read_line(&mut deadline).unwrap();
        let deadline = deadline.trim().to_string();
        print!("Теги (через запятую): ");
        io::stdout().flush().unwrap();
        let mut tags = String::new();
        reader.read_line(&mut tags).unwrap();
        let tags = tags.trim().to_string();
        let now = chrono::Local::now().format("%Y-%m-%dT%H:%M:%S").to_string();
        let task = Task {
            id: self.next_id,
            title: title.to_string(),
            description: desc.to_string(),
            priority,
            deadline,
            tags,
            column: "To Do".to_string(),
            created: now.clone(),
            updated: now,
            completed: "".to_string(),
        };
        self.next_id += 1;
        self.tasks.push(task);
        self.save();
        println!("Задача #{} добавлена в колонку 'To Do'", task.id);
    }

    fn list_tasks(&self) {
        for col in &self.columns {
            println!("\n{}=== {} ==={}", color::Fg(color::Blue), col, style::Reset);
            let mut found = false;
            for t in &self.tasks {
                if t.column == *col {
                    found = true;
                    let priority_color = match t.priority.as_str() {
                        "high" => color::Fg(color::Red),
                        "medium" => color::Fg(color::Yellow),
                        "low" => color::Fg(color::Green),
                        _ => color::Fg(color::White),
                    };
                    print!("#{} {}[{}]{} {}", t.id, priority_color, t.priority, style::Reset, t.title);
                    if !t.deadline.is_empty() {
                        print!(" (до {})", t.deadline);
                    }
                    if !t.tags.is_empty() {
                        print!(" [{}]", t.tags);
                    }
                    println!();
                }
            }
            if !found {
                println!("(пусто)");
            }
        }
    }

    fn move_task(&mut self) {
        print!("Номер задачи: ");
        io::stdout().flush().unwrap();
        let mut input = String::new();
        io::stdin().read_line(&mut input).unwrap();
        let id: i32 = input.trim().parse().unwrap_or(0);
        let task = self.find_task_mut(id);
        if task.is_none() {
            println!("Задача не найдена");
            return;
        }
        let task = task.unwrap();
        println!("Текущая колонка: {}", task.column);
        print!("В какую колонку переместить? ");
        io::stdout().flush().unwrap();
        let mut col = String::new();
        io::stdin().read_line(&mut col).unwrap();
        let col = col.trim();
        if col.is_empty() {
            println!("Колонка не указана");
            return;
        }
        if !self.columns.contains(&col.to_string()) {
            println!("Неверная колонка. Допустимые: {}", self.columns.join(", "));
            return;
        }
        task.column = col.to_string();
        task.updated = chrono::Local::now().format("%Y-%m-%dT%H:%M:%S").to_string();
        if task.column == "Done" {
            task.completed = task.updated.clone();
        }
        self.save();
        println!("Задача #{} перемещена в '{}'", task.id, task.column);
    }

    fn edit_task(&mut self) {
        print!("Номер задачи: ");
        io::stdout().flush().unwrap();
        let mut input = String::new();
        io::stdin().read_line(&mut input).unwrap();
        let id: i32 = input.trim().parse().unwrap_or(0);
        let task = self.find_task_mut(id);
        if task.is_none() {
            println!("Задача не найдена");
            return;
        }
        let task = task.unwrap();
        let reader = io::stdin();
        let mut reader = reader.lock();
        println!("Текущий заголовок: {}", task.title);
        print!("Новый заголовок (Enter для пропуска): ");
        io::stdout().flush().unwrap();
        let mut title = String::new();
        reader.read_line(&mut title).unwrap();
        let title = title.trim();
        if !title.is_empty() { task.title = title.to_string(); }
        println!("Текущее описание: {}", task.description);
        print!("Новое описание (Enter для пропуска): ");
        io::stdout().flush().unwrap();
        let mut desc = String::new();
        reader.read_line(&mut desc).unwrap();
        let desc = desc.trim();
        if !desc.is_empty() { task.description = desc.to_string(); }
        println!("Текущий приоритет: {}", task.priority);
        print!("Новый приоритет (Enter для пропуска): ");
        io::stdout().flush().unwrap();
        let mut priority = String::new();
        reader.read_line(&mut priority).unwrap();
        let priority = priority.trim();
        if !priority.is_empty() { task.priority = priority.to_string(); }
        println!("Текущий дедлайн: {}", task.deadline);
        print!("Новый дедлайн (Enter для пропуска): ");
        io::stdout().flush().unwrap();
        let mut deadline = String::new();
        reader.read_line(&mut deadline).unwrap();
        let deadline = deadline.trim();
        if !deadline.is_empty() { task.deadline = deadline.to_string(); }
        println!("Текущие теги: {}", task.tags);
        print!("Новые теги (Enter для пропуска): ");
        io::stdout().flush().unwrap();
        let mut tags = String::new();
        reader.read_line(&mut tags).unwrap();
        let tags = tags.trim();
        if !tags.is_empty() { task.tags = tags.to_string(); }
        task.updated = chrono::Local::now().format("%Y-%m-%dT%H:%M:%S").to_string();
        self.save();
        println!("Задача #{} обновлена", task.id);
    }

    fn delete_task(&mut self) {
        print!("Номер задачи: ");
        io::stdout().flush().unwrap();
        let mut input = String::new();
        io::stdin().read_line(&mut input).unwrap();
        let id: i32 = input.trim().parse().unwrap_or(0);
        if self.find_task(id).is_none() {
            println!("Задача не найдена");
            return;
        }
        print!("Удалить задачу #{}? (y/n): ", id);
        io::stdout().flush().unwrap();
        let mut ans = String::new();
        io::stdin().read_line(&mut ans).unwrap();
        if ans.trim().to_lowercase() == "y" {
            self.tasks.retain(|t| t.id != id);
            self.save();
            println!("Удалено");
        } else {
            println!("Отменено");
        }
    }

    fn search_tasks(&self) {
        print!("Введите текст для поиска: ");
        io::stdout().flush().unwrap();
        let mut query = String::new();
        io::stdin().read_line(&mut query).unwrap();
        let query = query.trim().to_lowercase();
        if query.is_empty() {
            println!("Текст не указан");
            return;
        }
        let found: Vec<&Task> = self.tasks.iter()
            .filter(|t| t.title.to_lowercase().contains(&query) || t.description.to_lowercase().contains(&query))
            .collect();
        if found.is_empty() {
            println!("Ничего не найдено");
            return;
        }
        println!("Найдено {} задач:", found.len());
        for t in found {
            println!("#{} [{}] {} (колонка: {})", t.id, t.priority, t.title, t.column);
        }
    }

    fn show_stats(&self) {
        let total = self.tasks.len();
        let mut col_counts = std::collections::HashMap::new();
        for c in &self.columns {
            col_counts.insert(c.clone(), 0);
        }
        let mut done = 0;
        let mut overdue = 0;
        let today = chrono::Local::now().format("%Y-%m-%d").to_string();
        for t in &self.tasks {
            *col_counts.entry(t.column.clone()).or_insert(0) += 1;
            if t.column == "Done" {
                done += 1;
            }
            if !t.deadline.is_empty() && t.deadline < today && t.column != "Done" {
                overdue += 1;
            }
        }
        println!("Всего задач: {}", total);
        for c in &self.columns {
            println!("{}: {}", c, col_counts.get(c).unwrap_or(&0));
        }
        println!("Выполнено: {}", done);
        println!("Просрочено: {}", overdue);
    }

    fn export_data(&self) {
        print!("Имя файла для экспорта (JSON): ");
        io::stdout().flush().unwrap();
        let mut fname = String::new();
        io::stdin().read_line(&mut fname).unwrap();
        let fname = fname.trim();
        let fname = if fname.is_empty() { "export.json" } else { fname };
        let data = serde_json::to_string_pretty(&self.tasks).unwrap();
        fs::write(fname, data).unwrap();
        println!("Экспортировано в {}", fname);
    }

    fn import_data(&mut self) {
        print!("Имя файла для импорта (JSON): ");
        io::stdout().flush().unwrap();
        let mut fname = String::new();
        io::stdin().read_line(&mut fname).unwrap();
        let fname = fname.trim();
        if fname.is_empty() {
            println!("Имя не указано");
            return;
        }
        let data = match fs::read_to_string(fname) {
            Ok(d) => d,
            Err(e) => { println!("Ошибка чтения: {}", e); return; }
        };
        let imported: Vec<Task> = match serde_json::from_str(&data) {
            Ok(v) => v,
            Err(_) => { println!("Ошибка формата JSON"); return; }
        };
        for mut t in imported {
            if t.id >= self.next_id {
                self.next_id = t.id + 1;
            }
            self.tasks.push(t);
        }
        self.save();
        println!("Импортировано {} задач", imported.len());
    }

    fn find_task(&self, id: i32) -> Option<&Task> {
        self.tasks.iter().find(|t| t.id == id)
    }

    fn find_task_mut(&mut self, id: i32) -> Option<&mut Task> {
        self.tasks.iter_mut().find(|t| t.id == id)
    }
}

fn main() {
    let mut app = App::new();
    let stdin = io::stdin();
    let mut reader = stdin.lock();
    println!("{}📋 KanbanFlow — Rust Edition{}", color::Fg(color::Cyan), style::Reset);
    println!("Колонки: {}", app.columns.join(", "));
    println!("Команды: add, list, move, edit, delete, search, stats, export, import, exit");
    loop {
        print!("{}> {}", color::Fg(color::Yellow), style::Reset);
        io::stdout().flush().unwrap();
        let mut cmd = String::new();
        if reader.read_line(&mut cmd).is_err() { break; }
        let cmd = cmd.trim();
        match cmd {
            "add" => app.add_task(),
            "list" => app.list_tasks(),
            "move" => app.move_task(),
            "edit" => app.edit_task(),
            "delete" => app.delete_task(),
            "search" => app.search_tasks(),
            "stats" => app.show_stats(),
            "export" => app.export_data(),
            "import" => app.import_data(),
            "exit" => {
                app.save();
                println!("До свидания!");
                break;
            }
            _ => println!("Неизвестная команда"),
        }
    }
}
