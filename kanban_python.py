# kanban_python.py — Менеджер задач (канбан) на Python (Tkinter GUI)

import tkinter as tk
from tkinter import ttk, messagebox, simpledialog, filedialog
import json
import os
import datetime
from collections import defaultdict
import threading
import time

class Task:
    def __init__(self, task_id, title, description="", priority="medium", deadline="", tags="", column="To Do"):
        self.id = task_id
        self.title = title
        self.description = description
        self.priority = priority  # high, medium, low
        self.deadline = deadline  # YYYY-MM-DD
        self.tags = tags
        self.column = column
        self.created = datetime.datetime.now().isoformat()
        self.updated = self.created
        self.completed = None

    def to_dict(self):
        return {
            "id": self.id,
            "title": self.title,
            "description": self.description,
            "priority": self.priority,
            "deadline": self.deadline,
            "tags": self.tags,
            "column": self.column,
            "created": self.created,
            "updated": self.updated,
            "completed": self.completed
        }

    @classmethod
    def from_dict(cls, data):
        task = cls(data["id"], data["title"], data["description"], data["priority"],
                   data["deadline"], data["tags"], data["column"])
        task.created = data.get("created", task.created)
        task.updated = data.get("updated", task.updated)
        task.completed = data.get("completed")
        return task

class KanbanApp:
    def __init__(self, root):
        self.root = root
        self.root.title("📋 KanbanFlow — Python")
        self.root.geometry("1000x650")
        self.columns = ["To Do", "In Progress", "Done"]
        self.tasks = []
        self.next_id = 1
        self.filename = "kanban.json"
        self.load_data()
        self.create_widgets()
        self.refresh_board()
        self.check_deadlines()

    def create_widgets(self):
        # Панель инструментов
        toolbar = tk.Frame(self.root)
        toolbar.pack(fill=tk.X, pady=5)
        tk.Button(toolbar, text="Добавить задачу", command=self.add_task).pack(side=tk.LEFT, padx=5)
        tk.Button(toolbar, text="Редактировать", command=self.edit_task).pack(side=tk.LEFT, padx=5)
        tk.Button(toolbar, text="Удалить", command=self.delete_task).pack(side=tk.LEFT, padx=5)
        tk.Button(toolbar, text="Переместить", command=self.move_task).pack(side=tk.LEFT, padx=5)
        tk.Button(toolbar, text="Поиск", command=self.search_tasks).pack(side=tk.LEFT, padx=5)
        tk.Button(toolbar, text="Статистика", command=self.show_stats).pack(side=tk.LEFT, padx=5)
        tk.Button(toolbar, text="Экспорт", command=self.export_data).pack(side=tk.LEFT, padx=5)
        tk.Button(toolbar, text="Импорт", command=self.import_data).pack(side=tk.LEFT, padx=5)

        # Фильтры
        filter_frame = tk.Frame(self.root)
        filter_frame.pack(fill=tk.X, pady=5)
        tk.Label(filter_frame, text="Приоритет:").pack(side=tk.LEFT, padx=5)
        self.priority_var = tk.StringVar()
        priority_combo = ttk.Combobox(filter_frame, textvariable=self.priority_var, values=["", "high", "medium", "low"], width=10)
        priority_combo.pack(side=tk.LEFT, padx=5)
        priority_combo.bind("<<ComboboxSelected>>", lambda e: self.refresh_board())
        tk.Label(filter_frame, text="Тег:").pack(side=tk.LEFT, padx=5)
        self.tag_var = tk.StringVar()
        self.tag_combo = ttk.Combobox(filter_frame, textvariable=self.tag_var, width=15)
        self.tag_combo.pack(side=tk.LEFT, padx=5)
        self.tag_combo.bind("<<ComboboxSelected>>", lambda e: self.refresh_board())
        tk.Label(filter_frame, text="Поиск:").pack(side=tk.LEFT, padx=5)
        self.search_var = tk.StringVar()
        self.search_var.trace("w", lambda *args: self.refresh_board())
        tk.Entry(filter_frame, textvariable=self.search_var, width=20).pack(side=tk.LEFT, padx=5)
        tk.Button(filter_frame, text="Сбросить", command=self.reset_filters).pack(side=tk.LEFT, padx=5)

        # Канбан-доска (фреймы для колонок)
        self.board_frame = tk.Frame(self.root)
        self.board_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=5)

        self.column_frames = {}
        for col in self.columns:
            frame = tk.LabelFrame(self.board_frame, text=col, padx=5, pady=5)
            frame.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=5)
            listbox = tk.Listbox(frame, height=20, selectmode=tk.SINGLE)
            listbox.pack(fill=tk.BOTH, expand=True)
            # Привязка для drag-n-drop (упрощённо - двойной клик для перемещения)
            listbox.bind("<Double-Button-1>", lambda e, col=col: self.move_task_from_listbox(col))
            self.column_frames[col] = {"frame": frame, "listbox": listbox}

        # Статус
        self.status = tk.Label(self.root, text="Готов", anchor=tk.W)
        self.status.pack(fill=tk.X, padx=10)

    def refresh_board(self, filter_tasks=None):
        # Очищаем все listbox
        for col in self.columns:
            self.column_frames[col]["listbox"].delete(0, tk.END)
        # Фильтруем
        tasks_to_show = filter_tasks if filter_tasks is not None else self.tasks
        priority_filter = self.priority_var.get()
        tag_filter = self.tag_var.get()
        search_text = self.search_var.get().strip().lower()
        for task in tasks_to_show:
            if priority_filter and task.priority != priority_filter:
                continue
            if tag_filter and tag_filter not in [t.strip() for t in task.tags.split(",") if t.strip()]:
                continue
            if search_text and search_text not in task.title.lower() and search_text not in task.description.lower():
                continue
            # Добавляем в соответствующую колонку
            listbox = self.column_frames[task.column]["listbox"]
            priority_color = {"high": "🔴", "medium": "🟡", "low": "🟢"}.get(task.priority, "⚪")
            display_text = f"#{task.id} {priority_color} {task.title}"
            if task.deadline:
                display_text += f" (до {task.deadline})"
            listbox.insert(tk.END, display_text)
            # Сохраняем id задачи как атрибут элемента (можно использовать словарь, но для простоты используем текст)
        self.update_status()

    def update_status(self):
        total = len(self.tasks)
        done = sum(1 for t in self.tasks if t.column == "Done")
        overdue = sum(1 for t in self.tasks if t.deadline and t.deadline < datetime.date.today().isoformat() and t.column != "Done")
        self.status.config(text=f"Всего: {total} | Выполнено: {done} | Просрочено: {overdue}")

    def reset_filters(self):
        self.priority_var.set("")
        self.tag_var.set("")
        self.search_var.set("")
        self.refresh_board()

    def add_task(self):
        dialog = tk.Toplevel(self.root)
        dialog.title("Добавить задачу")
        dialog.geometry("450x400")
        fields = {}
        labels = ["Заголовок", "Описание", "Приоритет (high/medium/low)", "Дедлайн (ГГГГ-ММ-ДД)", "Теги (через запятую)"]
        defaults = ["", "", "medium", "", ""]
        for i, lbl in enumerate(labels):
            tk.Label(dialog, text=lbl).grid(row=i, column=0, padx=5, pady=2, sticky="w")
            if lbl == "Описание":
                entry = tk.Text(dialog, width=30, height=4)
                entry.grid(row=i, column=1, padx=5, pady=2)
                fields[lbl] = entry
            else:
                entry = tk.Entry(dialog, width=30)
                if defaults[i]:
                    entry.insert(0, defaults[i])
                entry.grid(row=i, column=1, padx=5, pady=2)
                fields[lbl] = entry
        def save():
            title = fields["Заголовок"].get().strip()
            if not title:
                messagebox.showerror("Ошибка", "Введите заголовок")
                return
            description = fields["Описание"].get("1.0", tk.END).strip()
            priority = fields["Приоритет (high/medium/low)"].get().strip().lower()
            if priority not in ["high", "medium", "low"]:
                priority = "medium"
            deadline = fields["Дедлайн (ГГГГ-ММ-ДД)"].get().strip()
            tags = fields["Теги (через запятую)"].get().strip()
            task = Task(self.next_id, title, description, priority, deadline, tags)
            self.tasks.append(task)
            self.next_id += 1
            self.save_data()
            self.refresh_board()
            self.status.config(text=f"Добавлена задача #{task.id}")
            dialog.destroy()
        tk.Button(dialog, text="Сохранить", command=save).grid(row=len(labels), column=0, pady=10)
        tk.Button(dialog, text="Отмена", command=dialog.destroy).grid(row=len(labels), column=1, pady=10)

    def get_selected_task(self):
        # Определяем, какая колонка активна, ищем выбранный элемент
        for col in self.columns:
            listbox = self.column_frames[col]["listbox"]
            selection = listbox.curselection()
            if selection:
                # Из текста извлекаем id (после #)
                text = listbox.get(selection[0])
                try:
                    id_str = text.split()[0][1:]  # убираем '#' в начале
                    task_id = int(id_str)
                    for task in self.tasks:
                        if task.id == task_id:
                            return task
                except:
                    pass
        return None

    def edit_task(self):
        task = self.get_selected_task()
        if not task:
            messagebox.showinfo("Информация", "Выберите задачу")
            return
        dialog = tk.Toplevel(self.root)
        dialog.title("Редактировать задачу")
        dialog.geometry("450x400")
        fields = {}
        labels = ["Заголовок", "Описание", "Приоритет (high/medium/low)", "Дедлайн (ГГГГ-ММ-ДД)", "Теги"]
        defaults = [task.title, task.description, task.priority, task.deadline, task.tags]
        for i, lbl in enumerate(labels):
            tk.Label(dialog, text=lbl).grid(row=i, column=0, padx=5, pady=2, sticky="w")
            if lbl == "Описание":
                entry = tk.Text(dialog, width=30, height=4)
                entry.insert("1.0", defaults[i])
                entry.grid(row=i, column=1, padx=5, pady=2)
                fields[lbl] = entry
            else:
                entry = tk.Entry(dialog, width=30)
                entry.insert(0, defaults[i])
                entry.grid(row=i, column=1, padx=5, pady=2)
                fields[lbl] = entry
        def save():
            task.title = fields["Заголовок"].get().strip()
            task.description = fields["Описание"].get("1.0", tk.END).strip()
            task.priority = fields["Приоритет (high/medium/low)"].get().strip().lower()
            if task.priority not in ["high", "medium", "low"]:
                task.priority = "medium"
            task.deadline = fields["Дедлайн (ГГГГ-ММ-ДД)"].get().strip()
            task.tags = fields["Теги"].get().strip()
            task.updated = datetime.datetime.now().isoformat()
            self.save_data()
            self.refresh_board()
            self.status.config(text=f"Обновлена задача #{task.id}")
            dialog.destroy()
        tk.Button(dialog, text="Сохранить", command=save).grid(row=len(labels), column=0, pady=10)
        tk.Button(dialog, text="Отмена", command=dialog.destroy).grid(row=len(labels), column=1, pady=10)

    def delete_task(self):
        task = self.get_selected_task()
        if not task:
            messagebox.showinfo("Информация", "Выберите задачу")
            return
        if messagebox.askyesno("Удалить", f"Удалить задачу #{task.id}?"):
            self.tasks.remove(task)
            self.save_data()
            self.refresh_board()
            self.status.config(text=f"Удалена задача #{task.id}")

    def move_task(self):
        task = self.get_selected_task()
        if not task:
            messagebox.showinfo("Информация", "Выберите задачу")
            return
        col = simpledialog.askstring("Переместить", "В какую колонку?", initialvalue=task.column)
        if col and col in self.columns:
            task.column = col
            task.updated = datetime.datetime.now().isoformat()
            if col == "Done":
                task.completed = datetime.datetime.now().isoformat()
            self.save_data()
            self.refresh_board()
            self.status.config(text=f"Задача #{task.id} перемещена в '{col}'")

    def move_task_from_listbox(self, col):
        # Двойной клик для перемещения в следующую колонку
        task = self.get_selected_task()
        if not task:
            return
        # Определяем индекс текущей колонки
        try:
            idx = self.columns.index(task.column)
            if idx < len(self.columns) - 1:
                new_col = self.columns[idx+1]
                task.column = new_col
                if new_col == "Done":
                    task.completed = datetime.datetime.now().isoformat()
                self.save_data()
                self.refresh_board()
                self.status.config(text=f"Задача #{task.id} перемещена в '{new_col}'")
            else:
                messagebox.showinfo("Информация", "Задача уже в последней колонке")
        except ValueError:
            pass

    def search_tasks(self):
        query = simpledialog.askstring("Поиск", "Введите текст для поиска:")
        if query:
            query = query.lower()
            found = [t for t in self.tasks if query in t.title.lower() or query in t.description.lower()]
            if found:
                self.refresh_board(found)
                self.status.config(text=f"Найдено {len(found)} задач")
            else:
                messagebox.showinfo("Результат", "Ничего не найдено")

    def show_stats(self):
        total = len(self.tasks)
        col_counts = defaultdict(int)
        for t in self.tasks:
            col_counts[t.column] += 1
        done = col_counts.get("Done", 0)
        overdue = sum(1 for t in self.tasks if t.deadline and t.deadline < datetime.date.today().isoformat() and t.column != "Done")
        msg = f"Всего задач: {total}\n"
        for col in self.columns:
            msg += f"{col}: {col_counts.get(col, 0)}\n"
        msg += f"Выполнено: {done}\nПросрочено: {overdue}"
        messagebox.showinfo("Статистика", msg)

    def export_data(self):
        filename = filedialog.asksaveasfilename(defaultextension=".json", filetypes=[("JSON", "*.json")])
        if filename:
            data = [t.to_dict() for t in self.tasks]
            with open(filename, 'w', encoding='utf-8') as f:
                json.dump(data, f, indent=2, ensure_ascii=False)
            self.status.config(text=f"Экспортировано в {filename}")

    def import_data(self):
        filename = filedialog.askopenfilename(filetypes=[("JSON", "*.json")])
        if filename:
            with open(filename, 'r', encoding='utf-8') as f:
                data = json.load(f)
            for d in data:
                task = Task.from_dict(d)
                if task.id >= self.next_id:
                    self.next_id = task.id + 1
                self.tasks.append(task)
            self.save_data()
            self.refresh_board()
            self.status.config(text=f"Импортировано из {filename}")

    def load_data(self):
        if os.path.exists(self.filename):
            with open(self.filename, 'r', encoding='utf-8') as f:
                data = json.load(f)
            for d in data:
                task = Task.from_dict(d)
                if task.id >= self.next_id:
                    self.next_id = task.id + 1
                self.tasks.append(task)

    def save_data(self):
        data = [t.to_dict() for t in self.tasks]
        with open(self.filename, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)

    def check_deadlines(self):
        # Проверка при запуске
        today = datetime.date.today().isoformat()
        overdue = [t for t in self.tasks if t.deadline and t.deadline < today and t.column != "Done"]
        if overdue:
            msg = "Просроченные задачи:\n" + "\n".join(f"#{t.id} {t.title} (до {t.deadline})" for t in overdue)
            messagebox.showwarning("Просроченные задачи", msg)

if __name__ == "__main__":
    root = tk.Tk()
    app = KanbanApp(root)
    root.mainloop()
