using HomeLibrary.Controls;
using HomeLibrary.Data;
using HomeLibrary.Models;

namespace HomeLibrary.Forms;

/// <summary>Режим работы карточки книги.</summary>
public enum BookFormMode { Create, Edit, View }

/// <summary>
/// Карточка книги: создание / редактирование / просмотр.
/// Оглавление редактируется в HTML-редакторе и сохраняется в БД как XML.
/// Есть импорт/экспорт оглавления в виде XML-файла.
/// </summary>
public class BookForm : Form
{
    private readonly BookRepository _repo;
    private readonly Book _book;
    private readonly BookFormMode _mode;

    private readonly TextBox _txtTitle = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtAuthor = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown _numYear = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 3000 };
    private readonly TextBox _txtPublisher = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtIsbn = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtCategory = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtDescription = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 70 };
    private readonly HtmlEditor _tocEditor = new() { Dock = DockStyle.Fill };
    private readonly Button _btnSave = new() { Text = "Сохранить", AutoSize = true, MinimumSize = new Size(110, 0) };
    private readonly Button _btnCancel = new() { Text = "Отмена", AutoSize = true, MinimumSize = new Size(110, 0), DialogResult = DialogResult.Cancel };
    private readonly Button _btnImportToc = new() { Text = "Импорт XML…", AutoSize = true };
    private readonly Button _btnExportToc = new() { Text = "Экспорт XML…", AutoSize = true };

    /// <summary>
    /// Создаёт карточку книги.
    /// book = null — новая книга (режим Create), иначе загрузка существующей.
    /// </summary>
    public BookForm(BookRepository repo, Book? book, BookFormMode mode)
    {
        _repo = repo;
        _mode = mode;
        _book = book ?? new Book();

        Text = mode switch
        {
            BookFormMode.Create => "Новая книга",
            BookFormMode.Edit => $"Редактирование: {_book.Title}",
            _ => $"Просмотр: {_book.Title}"
        };
        Font = new Font("Segoe UI", 9.75f);
        MinimumSize = new Size(760, 640);
        StartPosition = FormStartPosition.CenterParent;

        SuspendLayout();

        BuildLayout();
        LoadData();

        ResumeLayout();
    }

    /// <summary>
    /// Enter при вводе в HTML-редакторе оглавления должен вставлять новую строку,
    /// а не нажимать «Сохранить» (AcceptButton) и закрывать карточку. Поэтому
    /// AcceptButton отключается, пока редактор (или многострочная «Аннотация»)
    /// содержит фокус, и возвращается, когда фокус уходит.
    /// </summary>
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        UpdateAcceptButton();
        // Отслеживаем перемещения фокуса по карточке
        foreach (Control c in GetAllControls(this))
            c.GotFocus -= OnControlGotFocus;
        foreach (Control c in GetAllControls(this))
            c.GotFocus += OnControlGotFocus;
    }

    private void OnControlGotFocus(object? sender, EventArgs e) => UpdateAcceptButton();

    /// <summary>
    /// AcceptButton активна, только если фокус НЕ в редакторе оглавления
    /// и НЕ в многострочном поле «Аннотация».
    /// </summary>
    private void UpdateAcceptButton()
    {
        bool inEditor = _tocEditor.ContainsFocus || _txtDescription.Focused;
        AcceptButton = inEditor ? null : _btnSave;
    }

    /// <summary>Все дочерние контролы формы (рекурсивно).</summary>
    private static IEnumerable<Control> GetAllControls(Control root)
    {
        foreach (Control c in root.Controls)
        {
            yield return c;
            foreach (var nested in GetAllControls(c))
                yield return nested;
        }
    }

    /// <summary>Строит раскладку карточки: поля книги, HTML-редактор оглавления, кнопки.</summary>
    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // поля книги
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // редактор оглавления
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // кнопки

        // --- панель полей книги: [подпись 120px][ввод][подпись 120px][ввод] ---
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // подпись слева
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));   // ввод слева
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // подпись справа
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));   // ввод справа

        AddField(fields, "Название:", _txtTitle, 3);
        AddField(fields, "Автор:", _txtAuthor, 1);
        AddField(fields, "Год издания:", _numYear, 1);
        AddField(fields, "Издательство:", _txtPublisher, 1);
        AddField(fields, "ISBN:", _txtIsbn, 1);
        AddField(fields, "Категория:", _txtCategory, 1);
        AddField(fields, "Аннотация:", _txtDescription, 1);

        // --- редактор оглавления ---
        var tocPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 12, 0, 8) };
        tocPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tocPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var tocLabel = new Label { Text = "Оглавление (HTML-редактор, сохраняется в XML):", AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
        tocPanel.Controls.Add(tocLabel, 0, 0);
        tocPanel.Controls.Add(_tocEditor, 0, 1);

        // --- кнопки: импорт/экспорт слева, Сохранить/Отмена справа ---
        var buttons = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 8, 0, 0) };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var leftButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        var rightButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };

        _btnImportToc.Click += (_, _) => ImportToc();
        _btnExportToc.Click += (_, _) => ExportToc();
        _btnSave.Click += (_, _) => Save();

        leftButtons.Controls.Add(_btnImportToc);
        leftButtons.Controls.Add(_btnExportToc);
        rightButtons.Controls.Add(_btnCancel);
        rightButtons.Controls.Add(_btnSave);

        buttons.Controls.Add(leftButtons, 0, 0);
        buttons.Controls.Add(rightButtons, 1, 0);

        root.Controls.Add(fields, 0, 0);
        root.Controls.Add(tocPanel, 0, 1);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);
    }

    private int _fieldRow;
    private bool _halfRowOpen;

    /// <summary>
    /// Добавляет поле на панель. columnSpan = 3 — на всю строку (label + input),
    /// columnSpan = 1 — половина строки (два поля в одну строку).
    /// </summary>
    private void AddField(TableLayoutPanel panel, string label, Control input, int columnSpan)
    {
        var lbl = new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(4),
            TextAlign = ContentAlignment.MiddleRight // подпись прижата к полю ввода
        };
        input.Margin = new Padding(4);
        input.Anchor = AnchorStyles.Left | AnchorStyles.Right;

        if (columnSpan == 3)
        {
            // Полная строка: label в колонке 0, input в колонках 1–3
            panel.Controls.Add(lbl, 0, _fieldRow);
            panel.Controls.Add(input, 1, _fieldRow);
            panel.SetColumnSpan(input, 3);
            _fieldRow++;
            _halfRowOpen = false;
        }
        else if (!_halfRowOpen)
        {
            // Левая половина строки
            panel.Controls.Add(lbl, 0, _fieldRow);
            panel.Controls.Add(input, 1, _fieldRow);
            _halfRowOpen = true;
        }
        else
        {
            // Правая половина строки
            panel.Controls.Add(lbl, 2, _fieldRow);
            panel.Controls.Add(input, 3, _fieldRow);
            _fieldRow++;
            _halfRowOpen = false;
        }
    }

    /// <summary>
    /// Заполняет поля карточки данными книги; в режиме просмотра
    /// переводит все контролы в «только чтение».
    /// </summary>
    private void LoadData()
    {
        _txtTitle.Text = _book.Title;
        _txtAuthor.Text = _book.Author;
        if (_book.PublishYear is int year && year > 0) _numYear.Value = year;
        _txtPublisher.Text = _book.Publisher ?? "";
        _txtIsbn.Text = _book.Isbn ?? "";
        _txtCategory.Text = _book.Category ?? "";
        _txtDescription.Text = _book.Description ?? "";
        _tocEditor.SetHtml(_book.TocHtml ?? "");

        if (_mode == BookFormMode.View)
        {
            Text = $"Просмотр: {_book.Title}";
            foreach (Control c in Controls)
                SetReadOnlyRecursive(c);
            _btnSave.Enabled = false;
            _btnImportToc.Enabled = false;
            _btnExportToc.Enabled = true;
            AcceptButton = _btnCancel;
        }
        else
        {
            AcceptButton = _btnSave;
            CancelButton = _btnCancel;
        }
    }

    /// <summary>
    /// Рекурсивно переводит все дочерние контролы в режим «только чтение».
    /// Кнопки «Отмена» и «Экспорт XML…» остаются активными.
    /// </summary>
    private static void SetReadOnlyRecursive(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            switch (c)
            {
                case TextBox tb: tb.ReadOnly = true; break;
                case NumericUpDown num: num.ReadOnly = true; break;
                case Button b when b.Text != "Отмена" && b.Text != "Экспорт XML…": b.Enabled = false; break;
            }
            SetReadOnlyRecursive(c);
        }
    }

    /// <summary>
    /// Валидирует поля и сохраняет книгу через хранимую процедуру
    /// (usp_Book_Insert для новой, usp_Book_Update для существующей).
    /// Оглавление берётся из HTML-редактора и упаковывается в XML.
    /// </summary>
    private void Save()
    {
        if (_mode == BookFormMode.View) return;

        if (string.IsNullOrWhiteSpace(_txtTitle.Text))
        {
            MessageBox.Show("Укажите название книги.", "Валидация", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtTitle.Focus();
            return;
        }
        if (string.IsNullOrWhiteSpace(_txtAuthor.Text))
        {
            MessageBox.Show("Укажите автора книги.", "Валидация", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtAuthor.Focus();
            return;
        }

        try
        {
            _book.Title = _txtTitle.Text.Trim();
            _book.Author = _txtAuthor.Text.Trim();
            _book.PublishYear = _numYear.Value > 0 ? (int)_numYear.Value : null;
            _book.Publisher = NullIfEmpty(_txtPublisher.Text);
            _book.Isbn = NullIfEmpty(_txtIsbn.Text);
            _book.Category = NullIfEmpty(_txtCategory.Text);
            _book.Description = NullIfEmpty(_txtDescription.Text);
            _book.TocHtml = NullIfEmpty(_tocEditor.GetHtml());

            if (_mode == BookFormMode.Create)
                _repo.Insert(_book);
            else
                _repo.Update(_book);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка сохранения:\r\n" + ex.Message, "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // -------- Импорт/экспорт оглавления в виде XML-файла --------

    /// <summary>
    /// Импорт оглавления из XML-файла: содержимое CDATA извлекается
    /// и загружается в HTML-редактор.
    /// </summary>
    private void ImportToc()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Импорт оглавления из XML-файла",
            Filter = "XML-файлы (*.xml)|*.xml|Все файлы (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var xml = File.ReadAllText(dlg.FileName);
            var html = TocXmlHelper.XmlToHtml(xml);
            _tocEditor.SetHtml(html);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось импортировать XML:\r\n" + ex.Message, "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Экспорт оглавления в XML-файл: HTML из редактора упаковывается
    /// в корневой элемент toc с CDATA.
    /// </summary>
    private void ExportToc()
    {
        using var dlg = new SaveFileDialog
        {
            Title = "Экспорт оглавления в XML-файл",
            Filter = "XML-файлы (*.xml)|*.xml",
            FileName = $"toc_{(_txtTitle.Text.Length > 0 ? SafeFileName(_txtTitle.Text) : "book")}.xml"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var html = _tocEditor.GetHtml();
            var xml = string.IsNullOrWhiteSpace(html) ? "<toc />" : TocXmlHelper.HtmlToXml(html);
            File.WriteAllText(dlg.FileName, xml);
            MessageBox.Show("Оглавление сохранено в XML-файл.", "Экспорт",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось экспортировать XML:\r\n" + ex.Message, "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>Возвращает null для пустой/пробельной строки, иначе обрезанную строку.</summary>
    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Заменяет недопустимые для имени файла символы на «_».</summary>
    private static string SafeFileName(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }
}
