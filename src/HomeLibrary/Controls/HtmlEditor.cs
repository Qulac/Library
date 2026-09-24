namespace HomeLibrary.Controls;

/// <summary>
/// Простой HTML-редактор на базе контрола WebBrowser (designMode = "On").
/// Панель инструментов работает через document.execCommand.
/// Используется для редактирования оглавления книги.
/// </summary>
public class HtmlEditor : UserControl
{
    private readonly WebBrowser _browser = new() { Dock = DockStyle.Fill };
    private readonly ToolStrip _toolbar = new() { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
    private readonly ToolStripButton _btnSource = new() { Text = "<>", ToolTipText = "Показать/скрыть HTML-код" };
    private readonly TextBox _sourceBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ScrollBars = ScrollBars.Both,
        Font = new Font("Consolas", 9.75f),
        Visible = false,
        AcceptsReturn = true,
        WordWrap = false
    };

    private string? _pendingHtml; // контент, ожидающий готовности документа
    private bool _ready;          // документ загружен и designMode включён

    // Отложенное применение контента: сразу после включения designMode
    // документ пересоздаётся и Body временно недоступен.
    private readonly System.Windows.Forms.Timer _applyTimer = new() { Interval = 50 };
    private int _applyAttempts;
    private const int MaxApplyAttempts = 40; // ~2 секунды ожидания

    /// <summary>Создаёт редактор: панель инструментов + WebBrowser в режиме правки.</summary>
    public HtmlEditor()
    {
        SuspendLayout();

        _browser.DocumentCompleted += OnDocumentCompleted;
        _applyTimer.Tick += (_, _) => TryApplyPendingHtml();

        AddToolbarButtons();
        _toolbar.Resize += (_, _) => { _toolbar.Height = 32; };

        Controls.Add(_browser);
        Controls.Add(_sourceBox);
        Controls.Add(_toolbar);

        // Инициализация пустого документа
        _browser.Navigate("about:blank");

        ResumeLayout();
    }

    /// <summary>Создаёт кнопки панели форматирования (B/I/U, заголовки, списки, «<>»).</summary>
    private void AddToolbarButtons()
    {
        _toolbar.Items.Add(MakeButton("B", "Полужирный", "bold", true));
        _toolbar.Items.Add(MakeButton("I", "Курсив", "italic", true));
        _toolbar.Items.Add(MakeButton("U", "Подчёркнутый", "underline", true));
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(MakeButton("Заг.", "Заголовок", "formatBlock", false, "h3"));
        _toolbar.Items.Add(MakeButton("¶", "Обычный абзац", "formatBlock", false, "p"));
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(MakeButton("• Список", "Маркированный список", "insertUnorderedList", false));
        _toolbar.Items.Add(MakeButton("1. Список", "Нумерованный список", "insertOrderedList", false));
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(MakeButton("Убрать формат", "Убрать форматирование", "removeFormat", false));
        _toolbar.Items.Add(new ToolStripSeparator());
        _btnSource.Click += OnToggleSource;
        _toolbar.Items.Add(_btnSource);
    }

    /// <summary>
    /// Создаёт кнопку, выполняющую команду document.execCommand при нажатии.
    /// command — имя команды; arg — необязательный аргумент (например, "h3" для formatBlock).
    /// </summary>
    private static ToolStripButton MakeButton(string text, string tooltip, string command, bool toggle, string? arg = null)
    {
        var btn = new ToolStripButton(text) { ToolTipText = tooltip, DisplayStyle = ToolStripItemDisplayStyle.Text };
        btn.Click += (_, _) =>
        {
            if (btn.Owner?.Parent is not HtmlEditor editor) return;
            editor.Exec(command, arg);
        };
        return btn;
    }

    /// <summary>Выполняет команду форматирования в документе редактора.</summary>
    private void Exec(string command, string? arg)
    {
        if (!_ready || _sourceBox.Visible) return;
        _browser.Document?.ExecCommand(command, false, (object?)arg ?? "");
        _browser.Focus();
    }

    /// <summary>
    /// Переключение «визуальный редактор ↔ исходный HTML-код».
    /// При возврате из режима кода разметка применяется в документ.
    /// </summary>
    private void OnToggleSource(object? sender, EventArgs e)
    {
        if (!_ready) return;
        if (_sourceBox.Visible)
        {
            // Применяем отредактированный HTML-код
            SetBodyHtml(_sourceBox.Text);
            _sourceBox.Visible = false;
            _browser.Visible = true;
        }
        else
        {
            _sourceBox.Text = GetBodyHtml();
            _browser.Visible = false;
            _sourceBox.Visible = true;
            _sourceBox.BringToFront();
            _sourceBox.Focus();
        }
    }

    private void OnDocumentCompleted(object? sender, WebBrowserDocumentCompletedEventArgs e)
    {
        if (_browser.ReadyState != WebBrowserReadyState.Complete) return;
        if (_ready) return;

        // Включаем режим редактирования (designMode = On).
        // Переключение designMode пересоздаёт DOM: Body документа становится
        // временно недоступен, поэтому отложенный контент применяется по таймеру.
        dynamic dom = _browser.Document!.DomDocument;
        dom.designMode = "On";
        _ready = true;

        _applyAttempts = 0;
        _applyTimer.Start();
    }

    /// <summary>
    /// Применяет отложенный контент, когда Body документа станет доступен.
    /// Если Body так и не появился, контент остаётся в _pendingHtml —
    /// GetHtml() вернёт его, и данные не потеряются.
    /// </summary>
    private void TryApplyPendingHtml()
    {
        if (_pendingHtml is null)
        {
            _applyTimer.Stop();
            return;
        }

        var body = _browser.Document?.Body;
        if (body is not null)
        {
            body.InnerHtml = _pendingHtml;
            _pendingHtml = null;
            _applyTimer.Stop();
            return;
        }

        if (++_applyAttempts >= MaxApplyAttempts)
            _applyTimer.Stop();
    }

    private void SetBodyHtml(string html)
    {
        if (!_ready)
        {
            _pendingHtml = html;
            return;
        }

        var body = _browser.Document?.Body;
        if (body is null)
        {
            // Body временно недоступен (DOM пересоздан после включения designMode) —
            // откладываем применение контента до его появления.
            _pendingHtml = html;
            _applyAttempts = 0;
            _applyTimer.Start();
            return;
        }
        body.InnerHtml = html;
    }

    private string GetBodyHtml()
    {
        if (_ready)
        {
            var bodyHtml = _browser.Document?.Body?.InnerHtml;
            if (bodyHtml is not null)
                return bodyHtml;
        }
        // Body ещё не готов или документ не загружен — не теряем отложенный контент
        return _pendingHtml ?? string.Empty;
    }

    /// <summary>Установить HTML-содержимое редактора.</summary>
    public void SetHtml(string html) => SetBodyHtml(html);

    /// <summary>Получить HTML-содержимое редактора.</summary>
    public string GetHtml()
    {
        if (_sourceBox.Visible)
            return _sourceBox.Text;
        return GetBodyHtml();
    }

    /// <summary>Сбросить содержимое редактора.</summary>
    public void Clear() => SetBodyHtml(string.Empty);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _applyTimer.Stop();
            _applyTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
