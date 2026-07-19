// kanban_java.java — Менеджер задач (канбан) на Java (Swing)

import javax.swing.*;
import javax.swing.event.*;
import java.awt.*;
import java.awt.event.*;
import java.io.*;
import java.nio.file.*;
import java.util.*;
import java.util.List;
import com.google.gson.*;

public class KanbanJava extends JFrame {
    private static final String DATA_FILE = "kanban.json";
    private List<Task> tasks = new ArrayList<>();
    private int nextId = 1;
    private String[] columns = {"To Do", "In Progress", "Done"};
    private JPanel boardPanel;
    private JList<String>[] columnLists;
    private DefaultListModel<String>[] listModels;
    private JTextField searchField;
    private JComboBox<String> priorityFilter, tagFilter;
    private JLabel statusLabel;
    private Map<String, Integer> columnIndex = new HashMap<>();

    public KanbanJava() {
        setTitle("📋 KanbanFlow — Java");
        setSize(1000, 650);
        setDefaultCloseOperation(EXIT_ON_CLOSE);
        setLayout(new BorderLayout());
        for (int i = 0; i < columns.length; i++) columnIndex.put(columns[i], i);
        loadData();
        createUI();
        refreshBoard();
        checkDeadlines();
    }

    private void createUI() {
        // Панель инструментов
        JPanel toolbar = new JPanel();
        JButton addBtn = new JButton("Добавить");
        JButton editBtn = new JButton("Редактировать");
        JButton delBtn = new JButton("Удалить");
        JButton moveBtn = new JButton("Переместить");
        JButton searchBtn = new JButton("Поиск");
        JButton statsBtn = new JButton("Статистика");
        JButton exportBtn = new JButton("Экспорт");
        JButton importBtn = new JButton("Импорт");
        toolbar.add(addBtn);
        toolbar.add(editBtn);
        toolbar.add(delBtn);
        toolbar.add(moveBtn);
        toolbar.add(searchBtn);
        toolbar.add(statsBtn);
        toolbar.add(exportBtn);
        toolbar.add(importBtn);
        add(toolbar, BorderLayout.NORTH);

        // Фильтры
        JPanel filterPanel = new JPanel(new FlowLayout());
        filterPanel.add(new JLabel("Приоритет:"));
        priorityFilter = new JComboBox<>(new String[]{"", "high", "medium", "low"});
        filterPanel.add(priorityFilter);
        filterPanel.add(new JLabel("Тег:"));
        tagFilter = new JComboBox<>();
        tagFilter.addItem("");
        filterPanel.add(tagFilter);
        filterPanel.add(new JLabel("Поиск:"));
        searchField = new JTextField(15);
        searchField.getDocument().addDocumentListener(new javax.swing.event.DocumentListener() {
            public void changedUpdate(javax.swing.event.DocumentEvent e) { refreshBoard(); }
            public void insertUpdate(javax.swing.event.DocumentEvent e) { refreshBoard(); }
            public void removeUpdate(javax.swing.event.DocumentEvent e) { refreshBoard(); }
        });
        filterPanel.add(searchField);
        JButton resetBtn = new JButton("Сбросить");
        resetBtn.addActionListener(e -> { priorityFilter.setSelectedIndex(0); tagFilter.setSelectedIndex(0); searchField.setText(""); });
        filterPanel.add(resetBtn);
        add(filterPanel, BorderLayout.SOUTH);

        // Канбан-доска
        boardPanel = new JPanel(new GridLayout(1, columns.length, 5, 5));
        listModels = new DefaultListModel[columns.length];
        columnLists = new JList[columns.length];
        for (int i = 0; i < columns.length; i++) {
            JPanel colPanel = new JPanel(new BorderLayout());
            colPanel.setBorder(BorderFactory.createTitledBorder(columns[i]));
            listModels[i] = new DefaultListModel<>();
            columnLists[i] = new JList<>(listModels[i]);
            columnLists[i].setSelectionMode(ListSelectionModel.SINGLE_SELECTION);
            columnLists[i].addMouseListener(new MouseAdapter() {
                public void mouseClicked(MouseEvent e) {
                    if (e.getClickCount() == 2) {
                        // переместить в следующую колонку
                        Task task = getSelectedTask();
                        if (task != null) {
                            int idx = columnIndex.get(task.column);
                            if (idx < columns.length - 1) {
                                task.column = columns[idx+1];
                                if (task.column.equals("Done")) task.completed = new java.text.SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss").format(new Date());
                                saveData();
                                refreshBoard();
                                statusLabel.setText("Задача #" + task.id + " перемещена в '" + task.column + "'");
                            }
                        }
                    }
                }
            });
            JScrollPane scroll = new JScrollPane(columnLists[i]);
            colPanel.add(scroll, BorderLayout.CENTER);
            boardPanel.add(colPanel);
        }
        add(boardPanel, BorderLayout.CENTER);

        // Статус
        statusLabel = new JLabel("Готов");
        add(statusLabel, BorderLayout.SOUTH);

        // Обработчики
        addBtn.addActionListener(e -> addTask());
        editBtn.addActionListener(e -> editTask());
        delBtn.addActionListener(e -> deleteTask());
        moveBtn.addActionListener(e -> moveTask());
        searchBtn.addActionListener(e -> searchTasks());
        statsBtn.addActionListener(e -> showStats());
        exportBtn.addActionListener(e -> exportData());
        importBtn.addActionListener(e -> importData());
        priorityFilter.addActionListener(e -> refreshBoard());
        tagFilter.addActionListener(e -> refreshBoard());
    }

    private void addTask() {
        JDialog dialog = new JDialog(this, "Добавить задачу", true);
        dialog.setLayout(new GridBagLayout());
        GridBagConstraints gbc = new GridBagConstraints();
        gbc.gridx = 0; gbc.gridy = 0; gbc.anchor = GridBagConstraints.WEST; gbc.insets = new Insets(5,5,5,5);
        dialog.add(new JLabel("Заголовок:"), gbc);
        JTextField titleField = new JTextField(20);
        gbc.gridx = 1; dialog.add(titleField, gbc);
        gbc.gridy++; gbc.gridx = 0; dialog.add(new JLabel("Описание:"), gbc);
        JTextArea descArea = new JTextArea(4, 20);
        JScrollPane descScroll = new JScrollPane(descArea);
        gbc.gridx = 1; dialog.add(descScroll, gbc);
        gbc.gridy++; gbc.gridx = 0; dialog.add(new JLabel("Приоритет:"), gbc);
        JComboBox<String> priorityCombo = new JComboBox<>(new String[]{"high", "medium", "low"});
        gbc.gridx = 1; dialog.add(priorityCombo, gbc);
        gbc.gridy++; gbc.gridx = 0; dialog.add(new JLabel("Дедлайн (ГГГГ-ММ-ДД):"), gbc);
        JTextField deadlineField = new JTextField(15);
        gbc.gridx = 1; dialog.add(deadlineField, gbc);
        gbc.gridy++; gbc.gridx = 0; dialog.add(new JLabel("Теги (через запятую):"), gbc);
        JTextField tagsField = new JTextField(20);
        gbc.gridx = 1; dialog.add(tagsField, gbc);
        gbc.gridy++; gbc.gridx = 0; gbc.gridwidth = 2; gbc.anchor = GridBagConstraints.CENTER;
        JButton saveBtn = new JButton("Сохранить");
        JButton cancelBtn = new JButton("Отмена");
        JPanel btnPanel = new JPanel();
        btnPanel.add(saveBtn);
        btnPanel.add(cancelBtn);
        dialog.add(btnPanel, gbc);
        dialog.pack();
        dialog.setLocationRelativeTo(this);
        saveBtn.addActionListener(e -> {
            String title = titleField.getText().trim();
            if (title.isEmpty()) { JOptionPane.showMessageDialog(dialog, "Введите заголовок"); return; }
            Task t = new Task();
            t.id = nextId++;
            t.title = title;
            t.description = descArea.getText().trim();
            t.priority = (String) priorityCombo.getSelectedItem();
            t.deadline = deadlineField.getText().trim();
            t.tags = tagsField.getText().trim();
            t.column = "To Do";
            t.created = new java.text.SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss").format(new Date());
            t.updated = t.created;
            t.completed = "";
            tasks.add(t);
            saveData();
            refreshBoard();
            statusLabel.setText("Добавлена задача #" + t.id);
            dialog.dispose();
        });
        cancelBtn.addActionListener(e -> dialog.dispose());
        dialog.setVisible(true);
    }

    private void editTask() {
        Task task = getSelectedTask();
        if (task == null) { JOptionPane.showMessageDialog(this, "Выберите задачу"); return; }
        JDialog dialog = new JDialog(this, "Редактировать задачу", true);
        dialog.setLayout(new GridBagLayout());
        GridBagConstraints gbc = new GridBagConstraints();
        gbc.insets = new Insets(5,5,5,5);
        gbc.gridx = 0; gbc.gridy = 0; gbc.anchor = GridBagConstraints.WEST;
        dialog.add(new JLabel("Заголовок:"), gbc);
        JTextField titleField = new JTextField(task.title, 20);
        gbc.gridx = 1; dialog.add(titleField, gbc);
        gbc.gridy++; gbc.gridx = 0; dialog.add(new JLabel("Описание:"), gbc);
        JTextArea descArea = new JTextArea(task.description, 4, 20);
        JScrollPane descScroll = new JScrollPane(descArea);
        gbc.gridx = 1; dialog.add(descScroll, gbc);
        gbc.gridy++; gbc.gridx = 0; dialog.add(new JLabel("Приоритет:"), gbc);
        JComboBox<String> priorityCombo = new JComboBox<>(new String[]{"high", "medium", "low"});
        priorityCombo.setSelectedItem(task.priority);
        gbc.gridx = 1; dialog.add(priorityCombo, gbc);
        gbc.gridy++; gbc.gridx = 0; dialog.add(new JLabel("Дедлайн:"), gbc);
        JTextField deadlineField = new JTextField(task.deadline, 15);
        gbc.gridx = 1; dialog.add(deadlineField, gbc);
        gbc.gridy++; gbc.gridx = 0; dialog.add(new JLabel("Теги:"), gbc);
        JTextField tagsField = new JTextField(task.tags, 20);
        gbc.gridx = 1; dialog.add(tagsField, gbc);
        gbc.gridy++; gbc.gridx = 0; gbc.gridwidth = 2; gbc.anchor = GridBagConstraints.CENTER;
        JButton saveBtn = new JButton("Сохранить");
        JButton cancelBtn = new JButton("Отмена");
        JPanel btnPanel = new JPanel();
        btnPanel.add(saveBtn);
        btnPanel.add(cancelBtn);
        dialog.add(btnPanel, gbc);
        dialog.pack();
        dialog.setLocationRelativeTo(this);
        saveBtn.addActionListener(e -> {
            task.title = titleField.getText().trim();
            task.description = descArea.getText().trim();
            task.priority = (String) priorityCombo.getSelectedItem();
            task.deadline = deadlineField.getText().trim();
            task.tags = tagsField.getText().trim();
            task.updated = new java.text.SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss").format(new Date());
            saveData();
            refreshBoard();
            statusLabel.setText("Обновлена задача #" + task.id);
            dialog.dispose();
        });
        cancelBtn.addActionListener(e -> dialog.dispose());
        dialog.setVisible(true);
    }

    private void deleteTask() {
        Task task = getSelectedTask();
        if (task == null) { JOptionPane.showMessageDialog(this, "Выберите задачу"); return; }
        if (JOptionPane.showConfirmDialog(this, "Удалить задачу?", "Подтверждение", JOptionPane.YES_NO_OPTION) == JOptionPane.YES_OPTION) {
            tasks.remove(task);
            saveData();
            refreshBoard();
            statusLabel.setText("Удалена задача #" + task.id);
        }
    }

    private void moveTask() {
        Task task = getSelectedTask();
        if (task == null) { JOptionPane.showMessageDialog(this, "Выберите задачу"); return; }
        String col = (String) JOptionPane.showInputDialog(this, "В какую колонку?", "Переместить",
                JOptionPane.QUESTION_MESSAGE, null, columns, task.column);
        if (col != null && !col.equals(task.column)) {
            task.column = col;
            task.updated = new java.text.SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss").format(new Date());
            if (col.equals("Done")) task.completed = task.updated;
            saveData();
            refreshBoard();
            statusLabel.setText("Задача #" + task.id + " перемещена в '" + col + "'");
        }
    }

    private void searchTasks() {
        String query = JOptionPane.showInputDialog(this, "Введите текст для поиска:");
        if (query == null) return;
        query = query.toLowerCase();
        List<Task> found = new ArrayList<>();
        for (Task t : tasks) {
            if (t.title.toLowerCase().contains(query) || t.description.toLowerCase().contains(query))
                found.add(t);
        }
        if (found.isEmpty()) {
            JOptionPane.showMessageDialog(this, "Ничего не найдено");
        } else {
            refreshBoard(found);
            statusLabel.setText("Найдено " + found.size() + " задач");
        }
    }

    private void showStats() {
        int total = tasks.size();
        int done = 0, overdue = 0;
        Map<String, Integer> colCounts = new HashMap<>();
        for (String col : columns) colCounts.put(col, 0);
        for (Task t : tasks) {
            colCounts.put(t.column, colCounts.getOrDefault(t.column, 0) + 1);
            if (t.column.equals("Done")) done++;
            if (!t.deadline.isEmpty() && t.deadline.compareTo(java.time.LocalDate.now().toString()) < 0 && !t.column.equals("Done"))
                overdue++;
        }
        StringBuilder msg = new StringBuilder("Всего задач: " + total + "\n");
        for (String col : columns) {
            msg.append(col).append(": ").append(colCounts.get(col)).append("\n");
        }
        msg.append("Выполнено: ").append(done).append("\n");
        msg.append("Просрочено: ").append(overdue);
        JOptionPane.showMessageDialog(this, msg.toString(), "Статистика", JOptionPane.INFORMATION_MESSAGE);
    }

    private void exportData() {
        JFileChooser chooser = new JFileChooser();
        if (chooser.showSaveDialog(this) == JFileChooser.APPROVE_OPTION) {
            File file = chooser.getSelectedFile();
            try (PrintWriter pw = new PrintWriter(file)) {
                Gson gson = new GsonBuilder().setPrettyPrinting().create();
                pw.write(gson.toJson(tasks));
                statusLabel.setText("Экспортировано в " + file.getName());
            } catch (IOException e) { e.printStackTrace(); }
        }
    }

    private void importData() {
        JFileChooser chooser = new JFileChooser();
        if (chooser.showOpenDialog(this) == JFileChooser.APPROVE_OPTION) {
            File file = chooser.getSelectedFile();
            try (Reader reader = new FileReader(file)) {
                Gson gson = new Gson();
                Task[] arr = gson.fromJson(reader, Task[].class);
                for (Task t : arr) {
                    if (t.id >= nextId) nextId = t.id + 1;
                    tasks.add(t);
                }
                saveData();
                refreshBoard();
                statusLabel.setText("Импортировано из " + file.getName());
            } catch (Exception e) { e.printStackTrace(); }
        }
    }

    private Task getSelectedTask() {
        for (int i = 0; i < columns.length; i++) {
            int idx = columnLists[i].getSelectedIndex();
            if (idx >= 0) {
                String item = listModels[i].getElementAt(idx);
                // извлекаем id
                int pos = item.indexOf('#');
                if (pos >= 0) {
                    int end = item.indexOf(' ', pos);
                    if (end == -1) end = item.length();
                    int id = Integer.parseInt(item.substring(pos+1, end));
                    for (Task t : tasks) {
                        if (t.id == id) return t;
                    }
                }
            }
        }
        return null;
    }

    private void refreshBoard() {
        refreshBoard(null);
    }

    private void refreshBoard(List<Task> filtered) {
        for (int i = 0; i < columns.length; i++) listModels[i].clear();
        String priority = (String) priorityFilter.getSelectedItem();
        String tag = (String) tagFilter.getSelectedItem();
        String search = searchField.getText().trim().toLowerCase();
        List<Task> display = filtered != null ? filtered : tasks;
        for (Task t : display) {
            if (!priority.isEmpty() && !t.priority.equals(priority)) continue;
            if (!tag.isEmpty() && !t.tags.contains(tag)) continue;
            if (!search.isEmpty() && !t.title.toLowerCase().contains(search) && !t.description.toLowerCase().contains(search)) continue;
            int idx = columnIndex.get(t.column);
            String displayText = "#" + t.id + " [" + t.priority + "] " + t.title;
            if (!t.deadline.isEmpty()) displayText += " (до " + t.deadline + ")";
            listModels[idx].addElement(displayText);
        }
        updateStatus();
        updateTagFilter();
    }

    private void updateStatus() {
        int total = tasks.size();
        int done = 0, overdue = 0;
        for (Task t : tasks) {
            if (t.column.equals("Done")) done++;
            if (!t.deadline.isEmpty() && t.deadline.compareTo(java.time.LocalDate.now().toString()) < 0 && !t.column.equals("Done"))
                overdue++;
        }
        statusLabel.setText("Всего: " + total + " | Выполнено: " + done + " | Просрочено: " + overdue);
    }

    private void updateTagFilter() {
        String current = (String) tagFilter.getSelectedItem();
        tagFilter.removeAllItems();
        tagFilter.addItem("");
        Set<String> allTags = new HashSet<>();
        for (Task t : tasks) {
            String[] tags = t.tags.split(",");
            for (String tg : tags) {
                tg = tg.trim();
                if (!tg.isEmpty()) allTags.add(tg);
            }
        }
        for (String tg : allTags) tagFilter.addItem(tg);
        if (current != null && allTags.contains(current)) tagFilter.setSelectedItem(current);
    }

    private void checkDeadlines() {
        String today = java.time.LocalDate.now().toString();
        List<Task> overdue = new ArrayList<>();
        for (Task t : tasks) {
            if (!t.deadline.isEmpty() && t.deadline.compareTo(today) < 0 && !t.column.equals("Done"))
                overdue.add(t);
        }
        if (!overdue.isEmpty()) {
            StringBuilder msg = new StringBuilder("Просроченные задачи:\n");
            for (Task t : overdue) msg.append("#").append(t.id).append(" ").append(t.title).append(" (до ").append(t.deadline).append(")\n");
            JOptionPane.showMessageDialog(this, msg.toString(), "Просроченные задачи", JOptionPane.WARNING_MESSAGE);
        }
    }

    private void loadData() {
        File file = new File(DATA_FILE);
        if (!file.exists()) return;
        try (Reader reader = new FileReader(file)) {
            Gson gson = new Gson();
            Task[] arr = gson.fromJson(reader, Task[].class);
            for (Task t : arr) {
                if (t.id >= nextId) nextId = t.id + 1;
                tasks.add(t);
            }
        } catch (Exception e) { /* ignore */ }
    }

    private void saveData() {
        try (PrintWriter pw = new PrintWriter(new File(DATA_FILE))) {
            Gson gson = new GsonBuilder().setPrettyPrinting().create();
            pw.write(gson.toJson(tasks));
        } catch (IOException e) { /* ignore */ }
    }

    static class Task {
        int id;
        String title;
        String description;
        String priority;
        String deadline;
        String tags;
        String column;
        String created;
        String updated;
        String completed;
    }

    public static void main(String[] args) throws Exception {
        UIManager.setLookAndFeel(UIManager.getSystemLookAndFeelClassName());
        SwingUtilities.invokeLater(() -> new KanbanJava().setVisible(true));
    }
}
