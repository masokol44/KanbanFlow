// kanban_cpp.cpp — Менеджер задач (канбан) на C++ (Qt Widgets)

#include <QApplication>
#include <QMainWindow>
#include <QWidget>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QPushButton>
#include <QListWidget>
#include <QLabel>
#include <QLineEdit>
#include <QComboBox>
#include <QMessageBox>
#include <QFileDialog>
#include <QFile>
#include <QJsonDocument>
#include <QJsonArray>
#include <QJsonObject>
#include <QDateTime>
#include <QInputDialog>
#include <QTabWidget>
#include <QScrollArea>

struct Task {
    int id;
    QString title;
    QString description;
    QString priority; // high, medium, low
    QString deadline; // YYYY-MM-DD
    QString tags;
    QString column;
    QString created;
    QString updated;
    QString completed;
};

class MainWindow : public QMainWindow {
    Q_OBJECT
public:
    MainWindow(QWidget *parent = nullptr) : QMainWindow(parent) {
        setWindowTitle("📋 KanbanFlow — C++");
        resize(1000, 650);
        loadData();
        createUI();
        refreshBoard();
    }

private slots:
    void addTask() {
        QDialog dialog(this);
        dialog.setWindowTitle("Добавить задачу");
        QFormLayout form(&dialog);
        QLineEdit *titleEdit = new QLineEdit;
        QTextEdit *descEdit = new QTextEdit;
        QComboBox *priorityCombo = new QComboBox;
        priorityCombo->addItems({"high", "medium", "low"});
        QLineEdit *deadlineEdit = new QLineEdit;
        QLineEdit *tagsEdit = new QLineEdit;
        form.addRow("Заголовок:", titleEdit);
        form.addRow("Описание:", descEdit);
        form.addRow("Приоритет:", priorityCombo);
        form.addRow("Дедлайн (ГГГГ-ММ-ДД):", deadlineEdit);
        form.addRow("Теги (через запятую):", tagsEdit);
        QDialogButtonBox buttons(QDialogButtonBox::Ok | QDialogButtonBox::Cancel);
        connect(&buttons, &QDialogButtonBox::accepted, &dialog, &QDialog::accept);
        connect(&buttons, &QDialogButtonBox::rejected, &dialog, &QDialog::reject);
        form.addRow(&buttons);
        if (dialog.exec() == QDialog::Accepted) {
            QString title = titleEdit->text().trimmed();
            if (title.isEmpty()) { QMessageBox::warning(this, "Ошибка", "Введите заголовок"); return; }
            Task t;
            t.id = nextId++;
            t.title = title;
            t.description = descEdit->toPlainText().trimmed();
            t.priority = priorityCombo->currentText();
            t.deadline = deadlineEdit->text().trimmed();
            t.tags = tagsEdit->text().trimmed();
            t.column = "To Do";
            t.created = QDateTime::currentDateTime().toString(Qt::ISODate);
            t.updated = t.created;
            t.completed = "";
            tasks.append(t);
            saveData();
            refreshBoard();
            statusLabel->setText("Добавлена задача #" + QString::number(t.id));
        }
    }

    void editTask() {
        Task *task = getSelectedTask();
        if (!task) { QMessageBox::information(this, "Информация", "Выберите задачу"); return; }
        QDialog dialog(this);
        dialog.setWindowTitle("Редактировать задачу");
        QFormLayout form(&dialog);
        QLineEdit *titleEdit = new QLineEdit(task->title);
        QTextEdit *descEdit = new QTextEdit(task->description);
        QComboBox *priorityCombo = new QComboBox;
        priorityCombo->addItems({"high", "medium", "low"});
        priorityCombo->setCurrentText(task->priority);
        QLineEdit *deadlineEdit = new QLineEdit(task->deadline);
        QLineEdit *tagsEdit = new QLineEdit(task->tags);
        form.addRow("Заголовок:", titleEdit);
        form.addRow("Описание:", descEdit);
        form.addRow("Приоритет:", priorityCombo);
        form.addRow("Дедлайн (ГГГГ-ММ-ДД):", deadlineEdit);
        form.addRow("Теги:", tagsEdit);
        QDialogButtonBox buttons(QDialogButtonBox::Ok | QDialogButtonBox::Cancel);
        connect(&buttons, &QDialogButtonBox::accepted, &dialog, &QDialog::accept);
        connect(&buttons, &QDialogButtonBox::rejected, &dialog, &QDialog::reject);
        form.addRow(&buttons);
        if (dialog.exec() == QDialog::Accepted) {
            task->title = titleEdit->text().trimmed();
            task->description = descEdit->toPlainText().trimmed();
            task->priority = priorityCombo->currentText();
            task->deadline = deadlineEdit->text().trimmed();
            task->tags = tagsEdit->text().trimmed();
            task->updated = QDateTime::currentDateTime().toString(Qt::ISODate);
            saveData();
            refreshBoard();
            statusLabel->setText("Обновлена задача #" + QString::number(task->id));
        }
    }

    void deleteTask() {
        Task *task = getSelectedTask();
        if (!task) { QMessageBox::information(this, "Информация", "Выберите задачу"); return; }
        if (QMessageBox::question(this, "Удалить", "Удалить задачу?") == QMessageBox::Yes) {
            tasks.erase(std::remove_if(tasks.begin(), tasks.end(),
                [task](const Task &t){ return t.id == task->id; }), tasks.end());
            saveData();
            refreshBoard();
            statusLabel->setText("Удалена задача #" + QString::number(task->id));
        }
    }

    void moveTask() {
        Task *task = getSelectedTask();
        if (!task) { QMessageBox::information(this, "Информация", "Выберите задачу"); return; }
        QStringList cols = {"To Do", "In Progress", "Done"};
        bool ok;
        QString col = QInputDialog::getItem(this, "Переместить", "В какую колонку?", cols, cols.indexOf(task->column), false, &ok);
        if (ok && !col.isEmpty()) {
            task->column = col;
            task->updated = QDateTime::currentDateTime().toString(Qt::ISODate);
            if (col == "Done") {
                task->completed = task->updated;
            }
            saveData();
            refreshBoard();
            statusLabel->setText(QString("Задача #%1 перемещена в '%2'").arg(task->id).arg(col));
        }
    }

    void searchTasks() {
        QString query = QInputDialog::getText(this, "Поиск", "Введите текст:");
        if (query.isEmpty()) { refreshBoard(); return; }
        query = query.toLower();
        QList<Task> found;
        for (const Task &t : tasks) {
            if (t.title.toLower().contains(query) || t.description.toLower().contains(query)) {
                found.append(t);
            }
        }
        refreshBoard(found);
        statusLabel->setText("Найдено " + QString::number(found.size()) + " задач");
    }

    void showStats() {
        int total = tasks.size();
        int done = 0, overdue = 0;
        QMap<QString, int> colCounts;
        for (const Task &t : tasks) {
            colCounts[t.column]++;
            if (t.column == "Done") done++;
            if (!t.deadline.isEmpty() && t.deadline < QDate::currentDate().toString("yyyy-MM-dd") && t.column != "Done")
                overdue++;
        }
        QString msg = "Всего задач: " + QString::number(total) + "\n";
        for (const QString &col : columns) {
            msg += col + ": " + QString::number(colCounts.value(col, 0)) + "\n";
        }
        msg += "Выполнено: " + QString::number(done) + "\n";
        msg += "Просрочено: " + QString::number(overdue);
        QMessageBox::information(this, "Статистика", msg);
    }

    void exportData() {
        QString filename = QFileDialog::getSaveFileName(this, "Экспорт JSON", "", "JSON (*.json)");
        if (filename.isEmpty()) return;
        QJsonArray arr;
        for (const Task &t : tasks) {
            QJsonObject obj;
            obj["id"] = t.id;
            obj["title"] = t.title;
            obj["description"] = t.description;
            obj["priority"] = t.priority;
            obj["deadline"] = t.deadline;
            obj["tags"] = t.tags;
            obj["column"] = t.column;
            obj["created"] = t.created;
            obj["updated"] = t.updated;
            obj["completed"] = t.completed;
            arr.append(obj);
        }
        QJsonDocument doc(arr);
        QFile file(filename);
        if (file.open(QIODevice::WriteOnly)) {
            file.write(doc.toJson());
            statusLabel->setText("Экспортировано в " + filename);
        }
    }

    void importData() {
        QString filename = QFileDialog::getOpenFileName(this, "Импорт JSON", "", "JSON (*.json)");
        if (filename.isEmpty()) return;
        QFile file(filename);
        if (!file.open(QIODevice::ReadOnly)) return;
        QByteArray data = file.readAll();
        QJsonDocument doc = QJsonDocument::fromJson(data);
        if (!doc.isArray()) { QMessageBox::warning(this, "Ошибка", "Неверный формат"); return; }
        QJsonArray arr = doc.array();
        for (const QJsonValue &v : arr) {
            QJsonObject obj = v.toObject();
            Task t;
            t.id = obj["id"].toInt();
            t.title = obj["title"].toString();
            t.description = obj["description"].toString();
            t.priority = obj["priority"].toString();
            t.deadline = obj["deadline"].toString();
            t.tags = obj["tags"].toString();
            t.column = obj["column"].toString();
            t.created = obj["created"].toString();
            t.updated = obj["updated"].toString();
            t.completed = obj["completed"].toString();
            if (t.id >= nextId) nextId = t.id + 1;
            tasks.append(t);
        }
        saveData();
        refreshBoard();
        statusLabel->setText("Импортировано из " + filename);
    }

private:
    QList<Task> tasks;
    int nextId = 1;
    QStringList columns = {"To Do", "In Progress", "Done"};
    QMap<QString, QListWidget*> columnWidgets;
    QLabel *statusLabel;
    QLineEdit *searchEdit;
    QComboBox *priorityFilter, *tagFilter;

    void createUI() {
        QWidget *central = new QWidget(this);
        setCentralWidget(central);
        QVBoxLayout *mainLayout = new QVBoxLayout(central);

        // Панель инструментов
        QHBoxLayout *toolbar = new QHBoxLayout();
        QPushButton *addBtn = new QPushButton("Добавить");
        QPushButton *editBtn = new QPushButton("Редактировать");
        QPushButton *delBtn = new QPushButton("Удалить");
        QPushButton *moveBtn = new QPushButton("Переместить");
        QPushButton *searchBtn = new QPushButton("Поиск");
        QPushButton *statsBtn = new QPushButton("Статистика");
        QPushButton *exportBtn = new QPushButton("Экспорт");
        QPushButton *importBtn = new QPushButton("Импорт");
        toolbar->addWidget(addBtn);
        toolbar->addWidget(editBtn);
        toolbar->addWidget(delBtn);
        toolbar->addWidget(moveBtn);
        toolbar->addWidget(searchBtn);
        toolbar->addWidget(statsBtn);
        toolbar->addWidget(exportBtn);
        toolbar->addWidget(importBtn);
        mainLayout->addLayout(toolbar);

        // Фильтры
        QHBoxLayout *filterLayout = new QHBoxLayout();
        filterLayout->addWidget(new QLabel("Приоритет:"));
        priorityFilter = new QComboBox;
        priorityFilter->addItem("");
        priorityFilter->addItems({"high", "medium", "low"});
        filterLayout->addWidget(priorityFilter);
        filterLayout->addWidget(new QLabel("Тег:"));
        tagFilter = new QComboBox;
        tagFilter->addItem("");
        filterLayout->addWidget(tagFilter);
        filterLayout->addWidget(new QLabel("Поиск:"));
        searchEdit = new QLineEdit;
        filterLayout->addWidget(searchEdit);
        QPushButton *resetBtn = new QPushButton("Сбросить");
        filterLayout->addWidget(resetBtn);
        mainLayout->addLayout(filterLayout);

        connect(priorityFilter, QOverload<int>::of(&QComboBox::currentIndexChanged), this, &MainWindow::refreshBoard);
        connect(tagFilter, QOverload<int>::of(&QComboBox::currentIndexChanged), this, &MainWindow::refreshBoard);
        connect(searchEdit, &QLineEdit::textChanged, this, &MainWindow::refreshBoard);
        connect(resetBtn, &QPushButton::clicked, [=](){ priorityFilter->setCurrentIndex(0); tagFilter->setCurrentIndex(0); searchEdit->clear(); });

        // Канбан-доска
        QHBoxLayout *boardLayout = new QHBoxLayout();
        for (const QString &col : columns) {
            QWidget *colWidget = new QWidget;
            QVBoxLayout *colLayout = new QVBoxLayout(colWidget);
            QLabel *colLabel = new QLabel(col);
            colLabel->setStyleSheet("font-weight: bold; font-size: 14px;");
            colLayout->addWidget(colLabel);
            QListWidget *list = new QListWidget;
            list->setSelectionMode(QAbstractItemView::SingleSelection);
            list->setDragDropMode(QAbstractItemView::InternalMove);
            // При двойном клике перемещаем в следующую колонку
            connect(list, &QListWidget::doubleClicked, [=]() {
                Task *task = getSelectedTask();
                if (!task) return;
                int idx = columns.indexOf(task->column);
                if (idx < columns.size()-1) {
                    task->column = columns[idx+1];
                    if (task->column == "Done") task->completed = QDateTime::currentDateTime().toString(Qt::ISODate);
                    saveData();
                    refreshBoard();
                    statusLabel->setText(QString("Задача #%1 перемещена в '%2'").arg(task->id).arg(task->column));
                }
            });
            colLayout->addWidget(list);
            boardLayout->addWidget(colWidget);
            columnWidgets[col] = list;
        }
        mainLayout->addLayout(boardLayout);

        // Статус
        statusLabel = new QLabel("Готов");
        mainLayout->addWidget(statusLabel);

        connect(addBtn, &QPushButton::clicked, this, &MainWindow::addTask);
        connect(editBtn, &QPushButton::clicked, this, &MainWindow::editTask);
        connect(delBtn, &QPushButton::clicked, this, &MainWindow::deleteTask);
        connect(moveBtn, &QPushButton::clicked, this, &MainWindow::moveTask);
        connect(searchBtn, &QPushButton::clicked, this, &MainWindow::searchTasks);
        connect(statsBtn, &QPushButton::clicked, this, &MainWindow::showStats);
        connect(exportBtn, &QPushButton::clicked, this, &MainWindow::exportData);
        connect(importBtn, &QPushButton::clicked, this, &MainWindow::importData);
    }

    Task* getSelectedTask() {
        for (const QString &col : columns) {
            QListWidget *list = columnWidgets[col];
            QListWidgetItem *item = list->currentItem();
            if (item) {
                QString text = item->text();
                // извлекаем id после '#'
                int pos = text.indexOf('#');
                if (pos != -1) {
                    int idStart = pos + 1;
                    int idEnd = text.indexOf(' ', idStart);
                    if (idEnd == -1) idEnd = text.length();
                    int id = text.mid(idStart, idEnd - idStart).toInt();
                    for (Task &t : tasks) {
                        if (t.id == id) return &t;
                    }
                }
            }
        }
        return nullptr;
    }

    void refreshBoard(const QList<Task> &filtered = QList<Task>()) {
        // Очищаем все listbox
        for (const QString &col : columns) {
            columnWidgets[col]->clear();
        }
        // Обновляем фильтры тегов
        updateTagFilter();

        QList<Task> display = filtered.isEmpty() ? tasks : filtered;
        // Применяем фильтры
        QString priority = priorityFilter->currentText();
        QString tag = tagFilter->currentText();
        QString search = searchEdit->text().trimmed().toLower();
        for (const Task &t : display) {
            if (!priority.isEmpty() && t.priority != priority) continue;
            if (!tag.isEmpty() && !t.tags.contains(tag)) continue;
            if (!search.isEmpty() && !t.title.toLower().contains(search) && !t.description.toLower().contains(search)) continue;
            // Добавляем в колонку
            QListWidget *list = columnWidgets[t.column];
            QString displayText = QString("#%1 [%2] %3").arg(t.id).arg(t.priority).arg(t.title);
            if (!t.deadline.isEmpty()) displayText += " (до " + t.deadline + ")";
            list->addItem(displayText);
        }
        updateStatus();
    }

    void updateStatus() {
        int total = tasks.size();
        int done = 0, overdue = 0;
        for (const Task &t : tasks) {
            if (t.column == "Done") done++;
            if (!t.deadline.isEmpty() && t.deadline < QDate::currentDate().toString("yyyy-MM-dd") && t.column != "Done") overdue++;
        }
        statusLabel->setText(QString("Всего: %1 | Выполнено: %2 | Просрочено: %3").arg(total).arg(done).arg(overdue));
    }

    void updateTagFilter() {
        QString current = tagFilter->currentText();
        tagFilter->clear();
        tagFilter->addItem("");
        QStringList allTags;
        for (const Task &t : tasks) {
            QStringList tags = t.tags.split(',', Qt::SkipEmptyParts);
            for (const QString &tg : tags) {
                QString trimmed = tg.trimmed();
                if (!trimmed.isEmpty() && !allTags.contains(trimmed)) allTags.append(trimmed);
            }
        }
        allTags.sort();
        tagFilter->addItems(allTags);
        int idx = tagFilter->findText(current);
        if (idx >= 0) tagFilter->setCurrentIndex(idx);
    }

    void loadData() {
        QFile file("kanban.json");
        if (!file.open(QIODevice::ReadOnly)) return;
        QByteArray data = file.readAll();
        QJsonDocument doc = QJsonDocument::fromJson(data);
        if (!doc.isArray()) return;
        QJsonArray arr = doc.array();
        for (const QJsonValue &v : arr) {
            QJsonObject obj = v.toObject();
            Task t;
            t.id = obj["id"].toInt();
            t.title = obj["title"].toString();
            t.description = obj["description"].toString();
            t.priority = obj["priority"].toString();
            t.deadline = obj["deadline"].toString();
            t.tags = obj["tags"].toString();
            t.column = obj["column"].toString();
            t.created = obj["created"].toString();
            t.updated = obj["updated"].toString();
            t.completed = obj["completed"].toString();
            if (t.id >= nextId) nextId = t.id + 1;
            tasks.append(t);
        }
    }

    void saveData() {
        QJsonArray arr;
        for (const Task &t : tasks) {
            QJsonObject obj;
            obj["id"] = t.id;
            obj["title"] = t.title;
            obj["description"] = t.description;
            obj["priority"] = t.priority;
            obj["deadline"] = t.deadline;
            obj["tags"] = t.tags;
            obj["column"] = t.column;
            obj["created"] = t.created;
            obj["updated"] = t.updated;
            obj["completed"] = t.completed;
            arr.append(obj);
        }
        QJsonDocument doc(arr);
        QFile file("kanban.json");
        if (file.open(QIODevice::WriteOnly)) {
            file.write(doc.toJson());
        }
    }
};

int main(int argc, char *argv[]) {
    QApplication app(argc, argv);
    MainWindow w;
    w.show();
    return app.exec();
}

#include "kanban_cpp.moc"
