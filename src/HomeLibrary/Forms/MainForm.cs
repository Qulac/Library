using HomeLibrary.Data;
using HomeLibrary.Models;

namespace HomeLibrary.Forms;

/// <summary>
/// Главная форма: список книг, поиск по названию/автору/оглавлению,
/// создание, редактирование, просмотр и удаление записей.
/// </summary>
public class MainForm : Form
{
    private readonly BookRepository _repo = new();

    private readonly TextBox _searchBox = new() { Dock = DockStyle.Fill, PlaceholderText = "Поиск по названию, автору, оглавлению…" };
    private readonly Button _btnSearch = new() { Text = "Найти", Dock = DockStyle.Fill };
    private readonly Button _btnShowAll = new() { Text = "Показать все", Dock = DockStyle.Fill };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
    private readonly Button _btnAdd = new() { Text = "Добавить", Dock = DockStyle.Fill };
    private readonly Button _btnEdit = new() { Text = "Редактировать", Dock = DockStyle.Fill };
    private readonly Button _btnView = new() { Text = "Просмотр", Dock = DockStyle.Fill };
    private readonly Button _btnDelete = new() { Text = "Удалить", Dock = DockStyle.Fill };
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusLabel = new() { Text = "Готово" };

    /// <summary>Создаёт главную форму: раскладка, привязка обработчиков, загрузка списка.</summary>
    public MainForm()
    {
        Text = "Домашняя библиотека";
        Font = new Font("Segoe UI", 9.75f);
        MinimumSize = new Size(900, 550);
        StartPosition = FormStartPosition.CenterScreen;

        SuspendLayout();

        // --- верхняя панель: поиск ---
        var searchPanel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 40, ColumnCount = 3, Padding = new Padding(4) };
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        _searchBox.KeyDown += OnSearchKeyDown;
        _btnSearch.Click += (_, _) => RunSearch();
        _btnShowAll.Click += (_, _) => ReloadBooks();
        searchPanel.Controls.Add(_searchBox, 0, 0);
        searchPanel.Controls.Add(_btnSearch, 1, 0);
        searchPanel.Controls.Add(_btnShowAll, 2, 0);

        // --- нижняя панель: кнопки ---
        var buttonPanel = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 44, ColumnCount = 4, Padding = new Padding(4) };
        for (int i = 0; i < 4; i++)
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        _btnAdd.Click += (_, _) => OpenCard(null, BookFormMode.Create);
        _btnEdit.Click += (_, _) => EditSelected();
        _btnView.Click += (_, _) => ViewSelected();
        _btnDelete.Click += (_, _) => DeleteSelected();
        buttonPanel.Controls.Add(_btnAdd, 0, 0);
        buttonPanel.Controls.Add(_btnEdit, 1, 0);
        buttonPanel.Controls.Add(_btnView, 2, 0);
        buttonPanel.Controls.Add(_btnDelete, 3, 0);

        // --- таблица ---
        _grid.Columns.Add("colId", "Id");
        _grid.Columns.Add("colTitle", "Название");
        _grid.Columns.Add("colAuthor", "Автор");
        _grid.Columns.Add("colYear", "Год издания");
        _grid.Columns.Add("colPublisher", "Издательство");
        _grid.Columns.Add("colCategory", "Категория");
        _grid.Columns["colId"]!.FillWeight = 6;
        _grid.Columns["colTitle"]!.FillWeight = 28;
        _grid.Columns["colAuthor"]!.FillWeight = 20;
        _grid.Columns["colYear"]!.FillWeight = 10;
        _grid.Columns["colPublisher"]!.FillWeight = 20;
        _grid.Columns["colCategory"]!.FillWeight = 16;
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };

        _status.Items.Add(_statusLabel);

        Controls.Add(_grid);
        Controls.Add(searchPanel);
        Controls.Add(buttonPanel);
        Controls.Add(_status);

        ResumeLayout();

        Load += (_, _) => ReloadBooks();
    }

    /// <summary>Enter в поле поиска запускает поиск (звук подавляется).</summary>
    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            RunSearch();
        }
    }

    /// <summary>
    /// Поиск книг по введённому термину (название / автор / оглавление).
    /// Пустой терм — показать все книги.
    /// </summary>
    private void RunSearch()
    {
        var term = _searchBox.Text.Trim();
        if (term.Length == 0)
        {
            ReloadBooks();
            return;
        }

        try
        {
            var books = _repo.Search(term);
            BindBooks(books);
            _statusLabel.Text = $"Найдено книг: {books.Count} (поиск: «{term}»)";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    /// <summary>Полная перезагрузка списка книг (usp_Book_GetAll).</summary>
    private void ReloadBooks()
    {
        try
        {
            var books = _repo.GetAll();
            BindBooks(books);
            _statusLabel.Text = $"Всего книг: {books.Count}";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    /// <summary>Заполняет таблицу списком книг.</summary>
    private void BindBooks(List<Book> books)
    {
        _grid.Rows.Clear();
        foreach (var b in books)
            _grid.Rows.Add(b.BookId, b.Title, b.Author, b.PublishYear, b.Publisher, b.Category);
    }

    /// <summary>
    /// Загружает выбранную книгу из БД (usp_Book_GetById).
    /// Если ничего не выбрано — показывает подсказку и возвращает null.
    /// </summary>
    private Book? SelectedBook()
    {
        if (_grid.CurrentRow is null)
        {
            MessageBox.Show("Выберите книгу в списке.", "Домашняя библиотека",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }
        var id = (int)_grid.CurrentRow.Cells["colId"].Value!;
        return _repo.GetById(id);
    }

    /// <summary>Открывает карточку выбранной книги в режиме редактирования.</summary>
    private void EditSelected()
    {
        var book = TryGetSelected();
        if (book is not null) OpenCard(book, BookFormMode.Edit);
    }

    /// <summary>Открывает карточку выбранной книги в режиме просмотра.</summary>
    private void ViewSelected()
    {
        var book = TryGetSelected();
        if (book is not null) OpenCard(book, BookFormMode.View);
    }

    /// <summary>Получает выбранную книгу с перехватом ошибок БД.</summary>
    private Book? TryGetSelected()
    {
        try
        {
            return SelectedBook();
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return null;
        }
    }

    /// <summary>
    /// Удаляет выбранную книгу (usp_Book_Delete) после подтверждения пользователя
    /// и обновляет список.
    /// </summary>
    private void DeleteSelected()
    {
        if (_grid.CurrentRow is null)
        {
            MessageBox.Show("Выберите книгу в списке.", "Домашняя библиотека",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var id = (int)_grid.CurrentRow.Cells["colId"].Value!;
        var title = (string)_grid.CurrentRow.Cells["colTitle"].Value!;
        if (MessageBox.Show($"Удалить книгу «{title}»?", "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        try
        {
            _repo.Delete(id);
            ReloadBooks();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    /// <summary>
    /// Открывает карточку книги в заданном режиме.
    /// После успешного сохранения список обновляется.
    /// </summary>
    private void OpenCard(Book? book, BookFormMode mode)
    {
        using var form = new BookForm(_repo, book, mode);
        if (form.ShowDialog(this) == DialogResult.OK)
            ReloadBooks();
    }

    /// <summary>Показывает сообщение об ошибке работы с БД.</summary>
    private static void ShowError(Exception ex) =>
        MessageBox.Show("Ошибка работы с базой данных:\r\n" + ex.Message, "Ошибка",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
}
