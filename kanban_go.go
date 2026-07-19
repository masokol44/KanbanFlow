// kanban_go.go — Менеджер задач (канбан) на Go (консоль)

package main

import (
	"bufio"
	"encoding/json"
	"fmt"
	"io/ioutil"
	"os"
	"strconv"
	"strings"
	"time"
)

type Task struct {
	Id          int    `json:"id"`
	Title       string `json:"title"`
	Description string `json:"description"`
	Priority    string `json:"priority"`
	Deadline    string `json:"deadline"`
	Tags        string `json:"tags"`
	Column      string `json:"column"`
	Created     string `json:"created"`
	Updated     string `json:"updated"`
	Completed   string `json:"completed"`
}

type App struct {
	tasks    []Task
	nextId   int
	columns  []string
	filename string
}

func NewApp() *App {
	return &App{
		columns:  []string{"To Do", "In Progress", "Done"},
		filename: "kanban.json",
	}
}

func (a *App) load() {
	data, err := ioutil.ReadFile(a.filename)
	if err != nil {
		return
	}
	var tasks []Task
	err = json.Unmarshal(data, &tasks)
	if err != nil {
		return
	}
	a.tasks = tasks
	for _, t := range a.tasks {
		if t.Id >= a.nextId {
			a.nextId = t.Id + 1
		}
	}
}

func (a *App) save() {
	data, _ := json.MarshalIndent(a.tasks, "", "  ")
	ioutil.WriteFile(a.filename, data, 0644)
}

func (a *App) addTask() {
	reader := bufio.NewReader(os.Stdin)
	fmt.Print("Заголовок: ")
	title, _ := reader.ReadString('\n')
	title = strings.TrimSpace(title)
	if title == "" { fmt.Println("Заголовок обязателен"); return }
	fmt.Print("Описание: ")
	desc, _ := reader.ReadString('\n')
	desc = strings.TrimSpace(desc)
	fmt.Print("Приоритет (high/medium/low): ")
	priority, _ := reader.ReadString('\n')
	priority = strings.TrimSpace(priority)
	if priority == "" { priority = "medium" }
	if priority != "high" && priority != "medium" && priority != "low" {
		priority = "medium"
	}
	fmt.Print("Дедлайн (ГГГГ-ММ-ДД): ")
	deadline, _ := reader.ReadString('\n')
	deadline = strings.TrimSpace(deadline)
	fmt.Print("Теги (через запятую): ")
	tags, _ := reader.ReadString('\n')
	tags = strings.TrimSpace(tags)
	task := Task{
		Id:          a.nextId,
		Title:       title,
		Description: desc,
		Priority:    priority,
		Deadline:    deadline,
		Tags:        tags,
		Column:      "To Do",
		Created:     time.Now().Format(time.RFC3339),
		Updated:     time.Now().Format(time.RFC3339),
	}
	a.nextId++
	a.tasks = append(a.tasks, task)
	a.save()
	fmt.Printf("Задача #%d добавлена в колонку 'To Do'\n", task.Id)
}

func (a *App) listTasks() {
	// Вывод по колонкам
	for _, col := range a.columns {
		fmt.Printf("\n=== %s ===\n", col)
		found := false
		for _, t := range a.tasks {
			if t.Column == col {
				found = true
				fmt.Printf("#%d [%s] %s", t.Id, t.Priority, t.Title)
				if t.Deadline != "" {
					fmt.Printf(" (до %s)", t.Deadline)
				}
				if t.Tags != "" {
					fmt.Printf(" [%s]", t.Tags)
				}
				fmt.Println()
			}
		}
		if !found {
			fmt.Println("(пусто)")
		}
	}
}

func (a *App) moveTask() {
	fmt.Print("Номер задачи: ")
	var id int
	fmt.Scanln(&id)
	task := a.findTask(id)
	if task == nil {
		fmt.Println("Задача не найдена")
		return
	}
	fmt.Printf("Текущая колонка: %s\n", task.Column)
	fmt.Print("В какую колонку переместить? ")
	var col string
	fmt.Scanln(&col)
	if col == "" {
		fmt.Println("Колонка не указана")
		return
	}
	// Проверяем, что колонка существует
	valid := false
	for _, c := range a.columns {
		if c == col {
			valid = true
			break
		}
	}
	if !valid {
		fmt.Println("Неверная колонка. Допустимые:", strings.Join(a.columns, ", "))
		return
	}
	task.Column = col
	task.Updated = time.Now().Format(time.RFC3339)
	if col == "Done" {
		task.Completed = task.Updated
	}
	a.save()
	fmt.Printf("Задача #%d перемещена в '%s'\n", task.Id, col)
}

func (a *App) editTask() {
	fmt.Print("Номер задачи: ")
	var id int
	fmt.Scanln(&id)
	task := a.findTask(id)
	if task == nil {
		fmt.Println("Задача не найдена")
		return
	}
	reader := bufio.NewReader(os.Stdin)
	fmt.Printf("Заголовок (%s): ", task.Title)
	title, _ := reader.ReadString('\n')
	title = strings.TrimSpace(title)
	if title != "" { task.Title = title }
	fmt.Printf("Описание (%s): ", task.Description)
	desc, _ := reader.ReadString('\n')
	desc = strings.TrimSpace(desc)
	if desc != "" { task.Description = desc }
	fmt.Printf("Приоритет (%s): ", task.Priority)
	priority, _ := reader.ReadString('\n')
	priority = strings.TrimSpace(priority)
	if priority != "" { task.Priority = priority }
	fmt.Printf("Дедлайн (%s): ", task.Deadline)
	deadline, _ := reader.ReadString('\n')
	deadline = strings.TrimSpace(deadline)
	if deadline != "" { task.Deadline = deadline }
	fmt.Printf("Теги (%s): ", task.Tags)
	tags, _ := reader.ReadString('\n')
	tags = strings.TrimSpace(tags)
	if tags != "" { task.Tags = tags }
	task.Updated = time.Now().Format(time.RFC3339)
	a.save()
	fmt.Printf("Задача #%d обновлена\n", task.Id)
}

func (a *App) deleteTask() {
	fmt.Print("Номер задачи: ")
	var id int
	fmt.Scanln(&id)
	task := a.findTask(id)
	if task == nil {
		fmt.Println("Задача не найдена")
		return
	}
	fmt.Printf("Удалить задачу #%d? (y/n): ", id)
	var ans string
	fmt.Scanln(&ans)
	if strings.ToLower(ans) == "y" {
		a.tasks = removeTask(a.tasks, id)
		a.save()
		fmt.Println("Удалено")
	} else {
		fmt.Println("Отменено")
	}
}

func (a *App) searchTasks() {
	reader := bufio.NewReader(os.Stdin)
	fmt.Print("Введите текст для поиска: ")
	query, _ := reader.ReadString('\n')
	query = strings.TrimSpace(query)
	if query == "" {
		fmt.Println("Текст не указан")
		return
	}
	query = strings.ToLower(query)
	found := []Task{}
	for _, t := range a.tasks {
		if strings.Contains(strings.ToLower(t.Title), query) || strings.Contains(strings.ToLower(t.Description), query) {
			found = append(found, t)
		}
	}
	if len(found) == 0 {
		fmt.Println("Ничего не найдено")
		return
	}
	fmt.Printf("Найдено %d задач:\n", len(found))
	for _, t := range found {
		fmt.Printf("#%d [%s] %s (колонка: %s)\n", t.Id, t.Priority, t.Title, t.Column)
	}
}

func (a *App) showStats() {
	total := len(a.tasks)
	colCounts := make(map[string]int)
	for _, c := range a.columns {
		colCounts[c] = 0
	}
	done := 0
	overdue := 0
	today := time.Now().Format("2006-01-02")
	for _, t := range a.tasks {
		colCounts[t.Column]++
		if t.Column == "Done" {
			done++
		}
		if t.Deadline != "" && t.Deadline < today && t.Column != "Done" {
			overdue++
		}
	}
	fmt.Printf("Всего задач: %d\n", total)
	for _, c := range a.columns {
		fmt.Printf("%s: %d\n", c, colCounts[c])
	}
	fmt.Printf("Выполнено: %d\n", done)
	fmt.Printf("Просрочено: %d\n", overdue)
}

func (a *App) exportData() {
	reader := bufio.NewReader(os.Stdin)
	fmt.Print("Имя файла для экспорта (JSON): ")
	fname, _ := reader.ReadString('\n')
	fname = strings.TrimSpace(fname)
	if fname == "" {
		fname = "export.json"
	}
	data, _ := json.MarshalIndent(a.tasks, "", "  ")
	ioutil.WriteFile(fname, data, 0644)
	fmt.Println("Экспортировано в", fname)
}

func (a *App) importData() {
	reader := bufio.NewReader(os.Stdin)
	fmt.Print("Имя файла для импорта (JSON): ")
	fname, _ := reader.ReadString('\n')
	fname = strings.TrimSpace(fname)
	if fname == "" {
		fmt.Println("Имя не указано")
		return
	}
	data, err := ioutil.ReadFile(fname)
	if err != nil {
		fmt.Println("Ошибка чтения:", err)
		return
	}
	var imported []Task
	err = json.Unmarshal(data, &imported)
	if err != nil {
		fmt.Println("Ошибка формата JSON")
		return
	}
	for _, t := range imported {
		if t.Id >= a.nextId {
			a.nextId = t.Id + 1
		}
		a.tasks = append(a.tasks, t)
	}
	a.save()
	fmt.Printf("Импортировано %d задач\n", len(imported))
}

func (a *App) findTask(id int) *Task {
	for i, t := range a.tasks {
		if t.Id == id {
			return &a.tasks[i]
		}
	}
	return nil
}

func removeTask(tasks []Task, id int) []Task {
	for i, t := range tasks {
		if t.Id == id {
			return append(tasks[:i], tasks[i+1:]...)
		}
	}
	return tasks
}

func main() {
	app := NewApp()
	app.load()
	reader := bufio.NewReader(os.Stdin)
	fmt.Println("📋 KanbanFlow — Go Edition")
	fmt.Println("Колонки:", strings.Join(app.columns, ", "))
	fmt.Println("Команды: add, list, move, edit, delete, search, stats, export, import, exit")
	for {
		fmt.Print("> ")
		cmd, _ := reader.ReadString('\n')
		cmd = strings.TrimSpace(cmd)
		switch cmd {
		case "add":
			app.addTask()
		case "list":
			app.listTasks()
		case "move":
			app.moveTask()
		case "edit":
			app.editTask()
		case "delete":
			app.deleteTask()
		case "search":
			app.searchTasks()
		case "stats":
			app.showStats()
		case "export":
			app.exportData()
		case "import":
			app.importData()
		case "exit":
			app.save()
			fmt.Println("До свидания!")
			return
		default:
			fmt.Println("Неизвестная команда")
		}
	}
}
