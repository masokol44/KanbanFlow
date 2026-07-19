// kanban_cs.cs — Менеджер задач (канбан) на C# (WPF)

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace KanbanFlowWPF
{
    public class Task : INotifyPropertyChanged
    {
        private int _id;
        private string _title;
        private string _description;
        private string _priority;
        private string _deadline;
        private string _tags;
        private string _column;
        private string _created;
        private string _updated;
        private string _completed;

        public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
        public string Title { get => _title; set { _title = value; OnPropertyChanged(); } }
        public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
        public string Priority { get => _priority; set { _priority = value; OnPropertyChanged(); } }
        public string Deadline { get => _deadline; set { _deadline = value; OnPropertyChanged(); } }
        public string Tags { get => _tags; set { _tags = value; OnPropertyChanged(); } }
        public string Column { get => _column; set { _column = value; OnPropertyChanged(); } }
        public string Created { get => _created; set { _created = value; OnPropertyChanged(); } }
        public string Updated { get => _updated; set { _updated = value; OnPropertyChanged(); } }
        public string Completed { get => _completed; set { _completed = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class MainWindow : Window
    {
        private ObservableCollection<Task> tasks = new ObservableCollection<Task>();
        private int nextId = 1;
        private string[] columns = { "To Do", "In Progress", "Done" };
        private string dataFile = "kanban.json";
        private ListBox[] columnLists;
        private TextBox searchBox;
        private ComboBox priorityFilter, tagFilter;
        private Label statusLabel;

        public MainWindow()
        {
            InitializeComponent();
            LoadData();
            CreateUI();
            RefreshBoard();
            CheckDeadlines();
        }

        private void CreateUI()
        {
            Title = "📋 KanbanFlow — C#";
            Width = 1000;
            Height = 650;
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Панель инструментов
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal };
            var addBtn = new Button { Content = "Добавить", Width = 80 };
            var editBtn = new Button { Content = "Редактировать", Width = 80 };
            var delBtn = new Button { Content = "Удалить", Width = 80 };
            var moveBtn = new Button { Content = "Переместить", Width = 80 };
            var searchBtn = new Button { Content = "Поиск", Width = 80 };
            var statsBtn = new Button { Content = "Статистика", Width = 80 };
            var exportBtn = new Button { Content = "Экспорт", Width = 80 };
            var importBtn = new Button { Content = "Импорт", Width = 80 };
            toolbar.Children.Add(addBtn);
            toolbar.Children.Add(editBtn);
            toolbar.Children.Add(delBtn);
            toolbar.Children.Add(moveBtn);
            toolbar.Children.Add(searchBtn);
            toolbar.Children.Add(statsBtn);
            toolbar.Children.Add(exportBtn);
            toolbar.Children.Add(importBtn);
            Grid.SetRow(toolbar, 0);
            grid.Children.Add(toolbar);

            // Фильтры
            var filterPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
            filterPanel.Children.Add(new Label { Content = "Приоритет:" });
            priorityFilter = new ComboBox { Width = 80 };
            priorityFilter.Items.Add("");
            priorityFilter.Items.Add("high");
            priorityFilter.Items.Add("medium");
            priorityFilter.Items.Add("low");
            filterPanel.Children.Add(priorityFilter);
            filterPanel.Children.Add(new Label { Content = "Тег:" });
            tagFilter = new ComboBox { Width = 100 };
            tagFilter.Items.Add("");
            filterPanel.Children.Add(tagFilter);
            filterPanel.Children.Add(new Label { Content = "Поиск:" });
            searchBox = new TextBox { Width = 150 };
            searchBox.TextChanged += (s, e) => RefreshBoard();
            filterPanel.Children.Add(searchBox);
            var resetBtn = new Button { Content = "Сбросить", Margin = new Thickness(5,0,0,0) };
            resetBtn.Click += (s, e) => { searchBox.Text = ""; priorityFilter.SelectedIndex = 0; tagFilter.SelectedIndex = 0; };
            filterPanel.Children.Add(resetBtn);
            Grid.SetRow(filterPanel, 1);
            grid.Children.Add(filterPanel);

            // Канбан-доска
            var boardPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            columnLists = new ListBox[columns.Length];
            for (int i = 0; i < columns.Length; i++)
            {
                var colPanel = new GroupBox { Header = columns[i], Width = 300, Margin = new Thickness(5) };
                var listBox = new ListBox { SelectionMode = SelectionMode.Single };
                listBox.MouseDoubleClick += (s, e) => {
                    // Переместить в следующую колонку
                    var task = GetSelectedTask();
                    if (task != null)
                    {
                        int idx = Array.IndexOf(columns, task.Column);
                        if (idx < columns.Length - 1)
                        {
                            task.Column = columns[idx + 1];
                            if (task.Column == "Done") task.Completed = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                            SaveData();
                            RefreshBoard();
                            statusLabel.Content = $"Задача #{task.Id} перемещена в '{task.Column}'";
                        }
                    }
                };
                colPanel.Content = listBox;
                boardPanel.Children.Add(colPanel);
                columnLists[i] = listBox;
            }
            Grid.SetRow(boardPanel, 2);
            grid.Children.Add(boardPanel);

            // Статус
            statusLabel = new Label { Content = "Готов" };
            Grid.SetRow(statusLabel, 3);
            grid.Children.Add(statusLabel);

            Content = grid;

            addBtn.Click += (s, e) => AddTask();
            editBtn.Click += (s, e) => EditTask();
            delBtn.Click += (s, e) => DeleteTask();
            moveBtn.Click += (s, e) => MoveTask();
            searchBtn.Click += (s, e) => SearchTasks();
            statsBtn.Click += (s, e) => ShowStats();
            exportBtn.Click += (s, e) => ExportData();
            importBtn.Click += (s, e) => ImportData();
            priorityFilter.SelectionChanged += (s, e) => RefreshBoard();
            tagFilter.SelectionChanged += (s, e) => RefreshBoard();
        }

        private void AddTask()
        {
            var dialog = new TaskDialog();
            if (dialog.ShowDialog() == true)
            {
                var task = dialog.Result;
                task.Id = nextId++;
                task.Column = "To Do";
                task.Created = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                task.Updated = task.Created;
                task.Completed = "";
                tasks.Add(task);
                SaveData();
                RefreshBoard();
                statusLabel.Content = $"Добавлена задача #{task.Id}";
            }
        }

        private void EditTask()
        {
            var task = GetSelectedTask();
            if (task == null) { MessageBox.Show("Выберите задачу"); return; }
            var dialog = new TaskDialog(task);
            if (dialog.ShowDialog() == true)
            {
                task.Title = dialog.Result.Title;
                task.Description = dialog.Result.Description;
                task.Priority = dialog.Result.Priority;
                task.Deadline = dialog.Result.Deadline;
                task.Tags = dialog.Result.Tags;
                task.Updated = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                SaveData();
                RefreshBoard();
                statusLabel.Content = $"Обновлена задача #{task.Id}";
            }
        }

        private void DeleteTask()
        {
            var task = GetSelectedTask();
            if (task == null) { MessageBox.Show("Выберите задачу"); return; }
            if (MessageBox.Show($"Удалить задачу #{task.Id}?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                tasks.Remove(task);
                SaveData();
                RefreshBoard();
                statusLabel.Content = $"Удалена задача #{task.Id}";
            }
        }

        private void MoveTask()
        {
            var task = GetSelectedTask();
            if (task == null) { MessageBox.Show("Выберите задачу"); return; }
            var col = (string)InputDialog.Show("Переместить", "В какую колонку?", columns, task.Column);
            if (col != null && col != task.Column)
            {
                task.Column = col;
                task.Updated = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                if (col == "Done") task.Completed = task.Updated;
                SaveData();
                RefreshBoard();
                statusLabel.Content = $"Задача #{task.Id} перемещена в '{col}'";
            }
        }

        private void SearchTasks()
        {
            var query = InputDialog.Show("Поиск", "Введите текст для поиска:", "");
            if (query == null) return;
            query = query.ToLower();
            var found = tasks.Where(t => t.Title.ToLower().Contains(query) || t.Description.ToLower().Contains(query)).ToList();
            if (found.Count == 0) MessageBox.Show("Ничего не найдено");
            else
            {
                RefreshBoard(found);
                statusLabel.Content = $"Найдено {found.Count} задач";
            }
        }

        private void ShowStats()
        {
            int total = tasks.Count;
            int done = tasks.Count(t => t.Column == "Done");
            int overdue = tasks.Count(t => !string.IsNullOrEmpty(t.Deadline) && t.Deadline.CompareTo(DateTime.Today.ToString("yyyy-MM-dd")) < 0 && t.Column != "Done");
            var colCounts = columns.ToDictionary(c => c, c => tasks.Count(t => t.Column == c));
            string msg = $"Всего задач: {total}\n";
            foreach (var c in columns) msg += $"{c}: {colCounts[c]}\n";
            msg += $"Выполнено: {done}\nПросрочено: {overdue}";
            MessageBox.Show(msg, "Статистика");
        }

        private void ExportData()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "JSON (*.json)|*.json" };
            if (dialog.ShowDialog() == true)
            {
                string json = JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dialog.FileName, json);
                statusLabel.Content = $"Экспортировано в {dialog.FileName}";
            }
        }

        private void ImportData()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "JSON (*.json)|*.json" };
            if (dialog.ShowDialog() == true)
            {
                string json = File.ReadAllText(dialog.FileName);
                var imported = JsonSerializer.Deserialize<List<Task>>(json);
                if (imported != null)
                {
                    foreach (var t in imported)
                    {
                        if (t.Id >= nextId) nextId = t.Id + 1;
                        tasks.Add(t);
                    }
                    SaveData();
                    RefreshBoard();
                    statusLabel.Content = $"Импортировано из {dialog.FileName}";
                }
            }
        }

        private Task GetSelectedTask()
        {
            foreach (var list in columnLists)
            {
                if (list.SelectedItem != null)
                {
                    string item = list.SelectedItem.ToString();
                    // извлекаем id
                    int pos = item.IndexOf('#');
                    if (pos >= 0)
                    {
                        int end = item.IndexOf(' ', pos);
                        if (end == -1) end = item.Length;
                        int id = int.Parse(item.Substring(pos+1, end-pos-1));
                        return tasks.FirstOrDefault(t => t.Id == id);
                    }
                }
            }
            return null;
        }

        private void RefreshBoard(List<Task> filtered = null)
        {
            // Очищаем списки
            foreach (var list in columnLists) list.Items.Clear();
            // Фильтры
            string priority = priorityFilter.SelectedItem as string;
            string tag = tagFilter.SelectedItem as string;
            string search = searchBox.Text.Trim().ToLower();
            var display = filtered ?? tasks;
            foreach (var t in display)
            {
                if (!string.IsNullOrEmpty(priority) && t.Priority != priority) continue;
                if (!string.IsNullOrEmpty(tag) && !t.Tags.Contains(tag)) continue;
                if (!string.IsNullOrEmpty(search) && !t.Title.ToLower().Contains(search) && !t.Description.ToLower().Contains(search)) continue;
                int idx = Array.IndexOf(columns, t.Column);
                string displayText = $"#{t.Id} [{t.Priority}] {t.Title}";
                if (!string.IsNullOrEmpty(t.Deadline)) displayText += $" (до {t.Deadline})";
                columnLists[idx].Items.Add(displayText);
            }
            UpdateStatus();
            UpdateTagFilter();
        }

        private void UpdateStatus()
        {
            int total = tasks.Count;
            int done = tasks.Count(t => t.Column == "Done");
            int overdue = tasks.Count(t => !string.IsNullOrEmpty(t.Deadline) && t.Deadline.CompareTo(DateTime.Today.ToString("yyyy-MM-dd")) < 0 && t.Column != "Done");
            statusLabel.Content = $"Всего: {total} | Выполнено: {done} | Просрочено: {overdue}";
        }

        private void UpdateTagFilter()
        {
            string current = tagFilter.SelectedItem as string;
            tagFilter.Items.Clear();
            tagFilter.Items.Add("");
            var allTags = tasks.SelectMany(t => t.Tags.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                               .Select(t => t.Trim())
                               .Where(t => !string.IsNullOrEmpty(t))
                               .Distinct()
                               .OrderBy(t => t);
            foreach (var tg in allTags) tagFilter.Items.Add(tg);
            if (!string.IsNullOrEmpty(current) && tagFilter.Items.Contains(current)) tagFilter.SelectedItem = current;
        }

        private void CheckDeadlines()
        {
            string today = DateTime.Today.ToString("yyyy-MM-dd");
            var overdue = tasks.Where(t => !string.IsNullOrEmpty(t.Deadline) && t.Deadline.CompareTo(today) < 0 && t.Column != "Done").ToList();
            if (overdue.Any())
            {
                string msg = "Просроченные задачи:\n" + string.Join("\n", overdue.Select(t => $"#{t.Id} {t.Title} (до {t.Deadline})"));
                MessageBox.Show(msg, "Просроченные задачи", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadData()
        {
            if (File.Exists(dataFile))
            {
                string json = File.ReadAllText(dataFile);
                var list = JsonSerializer.Deserialize<List<Task>>(json);
                if (list != null)
                {
                    foreach (var t in list)
                    {
                        if (t.Id >= nextId) nextId = t.Id + 1;
                        tasks.Add(t);
                    }
                }
            }
        }

        private void SaveData()
        {
            string json = JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dataFile, json);
        }

        public class TaskDialog : Window
        {
            public Task Result { get; private set; }

            public TaskDialog(Task editTask = null)
            {
                Title = editTask == null ? "Добавить задачу" : "Редактировать задачу";
                Width = 450;
                Height = 400;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                var panel = new StackPanel { Margin = new Thickness(10) };
                panel.Children.Add(new Label { Content = "Заголовок:" });
                var titleBox = new TextBox();
                panel.Children.Add(titleBox);
                panel.Children.Add(new Label { Content = "Описание:" });
                var descBox = new TextBox { TextWrapping = TextWrapping.Wrap, Height = 60 };
                panel.Children.Add(descBox);
                panel.Children.Add(new Label { Content = "Приоритет (high/medium/low):" });
                var priorityCombo = new ComboBox();
                priorityCombo.Items.Add("high");
                priorityCombo.Items.Add("medium");
                priorityCombo.Items.Add("low");
                priorityCombo.SelectedIndex = 1;
                panel.Children.Add(priorityCombo);
                panel.Children.Add(new Label { Content = "Дедлайн (ГГГГ-ММ-ДД):" });
                var deadlineBox = new TextBox();
                panel.Children.Add(deadlineBox);
                panel.Children.Add(new Label { Content = "Теги (через запятую):" });
                var tagsBox = new TextBox();
                panel.Children.Add(tagsBox);
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,10,0,0) };
                var okBtn = new Button { Content = "OK", Width = 80, Margin = new Thickness(5) };
                var cancelBtn = new Button { Content = "Отмена", Width = 80, Margin = new Thickness(5) };
                buttons.Children.Add(okBtn);
                buttons.Children.Add(cancelBtn);
                panel.Children.Add(buttons);
                Content = panel;

                if (editTask != null)
                {
                    titleBox.Text = editTask.Title;
                    descBox.Text = editTask.Description;
                    priorityCombo.SelectedItem = editTask.Priority;
                    deadlineBox.Text = editTask.Deadline;
                    tagsBox.Text = editTask.Tags;
                }

                okBtn.Click += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(titleBox.Text)) { MessageBox.Show("Введите заголовок"); return; }
                    Result = new Task
                    {
                        Title = titleBox.Text.Trim(),
                        Description = descBox.Text.Trim(),
                        Priority = priorityCombo.SelectedItem as string ?? "medium",
                        Deadline = deadlineBox.Text.Trim(),
                        Tags = tagsBox.Text.Trim(),
                        // Id, Column, Created будут установлены в MainWindow
                    };
                    DialogResult = true;
                    Close();
                };
                cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
            }
        }

        public static class InputDialog
        {
            public static string Show(string title, string prompt, string[] items = null, string defaultItem = null)
            {
                if (items != null)
                {
                    var dialog = new Window { Title = title, Width = 300, Height = 150, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                    var panel = new StackPanel { Margin = new Thickness(10) };
                    panel.Children.Add(new Label { Content = prompt });
                    var combo = new ComboBox { ItemsSource = items, SelectedItem = defaultItem };
                    panel.Children.Add(combo);
                    var okBtn = new Button { Content = "OK", Width = 80, Margin = new Thickness(5) };
                    var cancelBtn = new Button { Content = "Отмена", Width = 80 };
                    var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                    btns.Children.Add(okBtn);
                    btns.Children.Add(cancelBtn);
                    panel.Children.Add(btns);
                    dialog.Content = panel;
                    string result = null;
                    okBtn.Click += (s, e) => { result = combo.SelectedItem as string; dialog.DialogResult = true; dialog.Close(); };
                    cancelBtn.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };
                    return dialog.ShowDialog() == true ? result : null;
                }
                else
                {
                    string result = Microsoft.VisualBasic.Interaction.InputBox(prompt, title);
                    return string.IsNullOrEmpty(result) ? null : result;
                }
            }
        }

        [STAThread]
        static void Main()
        {
            var app = new Application();
            app.Run(new MainWindow());
        }
    }
}
