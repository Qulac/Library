using System.Data;
using HomeLibrary.Models;
using Microsoft.Data.SqlClient;

namespace HomeLibrary.Data;

/// <summary>
/// Репозиторий книг. Вся работа с данными выполняется ТОЛЬКО через хранимые процедуры
/// (usp_Book_Insert / usp_Book_Update / usp_Book_Delete / usp_Book_GetById /
///  usp_Book_GetAll / usp_Book_Search).
/// </summary>
public class BookRepository
{
    // ---------------- INSERT ----------------

    /// <summary>Добавляет книгу (usp_Book_Insert). Возвращает Id новой записи.</summary>
    public int Insert(Book book)
    {
        using var conn = Db.OpenConnection();
        using var cmd = new SqlCommand("dbo.usp_Book_Insert", conn) { CommandType = CommandType.StoredProcedure };
        FillParameters(cmd, book);
        var idParam = cmd.Parameters.Add("@BookId", SqlDbType.Int);
        idParam.Direction = ParameterDirection.Output;
        cmd.ExecuteNonQuery();
        return (int)idParam.Value!;
    }

    // ---------------- UPDATE ----------------

    /// <summary>Изменяет книгу по BookId (usp_Book_Update).</summary>
    public void Update(Book book)
    {
        using var conn = Db.OpenConnection();
        using var cmd = new SqlCommand("dbo.usp_Book_Update", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@BookId", book.BookId);
        FillParameters(cmd, book);
        cmd.ExecuteNonQuery();
    }

    // ---------------- DELETE ----------------

    /// <summary>Удаляет книгу по BookId (usp_Book_Delete).</summary>
    public void Delete(int bookId)
    {
        using var conn = Db.OpenConnection();
        using var cmd = new SqlCommand("dbo.usp_Book_Delete", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@BookId", bookId);
        cmd.ExecuteNonQuery();
    }

    // ---------------- SELECT BY ID ----------------

    /// <summary>Возвращает книгу по Id (usp_Book_GetById) или null, если не найдена.</summary>
    public Book? GetById(int bookId)
    {
        using var conn = Db.OpenConnection();
        using var cmd = new SqlCommand("dbo.usp_Book_GetById", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@BookId", bookId);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapBook(reader) : null;
    }

    // ---------------- SELECT ALL ----------------

    /// <summary>Возвращает все книги, отсортированные по названию (usp_Book_GetAll).</summary>
    public List<Book> GetAll()
    {
        using var conn = Db.OpenConnection();
        using var cmd = new SqlCommand("dbo.usp_Book_GetAll", conn) { CommandType = CommandType.StoredProcedure };
        using var reader = cmd.ExecuteReader();
        var books = new List<Book>();
        while (reader.Read())
            books.Add(MapBook(reader));
        return books;
    }

    // ---------------- SEARCH (название / автор / оглавление) ----------------

    /// <summary>Поиск книг по подстроке в названии, авторе или оглавлении (usp_Book_Search).</summary>
    public List<Book> Search(string term)
    {
        using var conn = Db.OpenConnection();
        using var cmd = new SqlCommand("dbo.usp_Book_Search", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@SearchTerm", term);
        using var reader = cmd.ExecuteReader();
        var books = new List<Book>();
        while (reader.Read())
            books.Add(MapBook(reader));
        return books;
    }

    // ---------------- helpers ----------------

    /// <summary>Заполняет параметры команды значениями книги (NULL — через DBNull.Value).</summary>
    private static void FillParameters(SqlCommand cmd, Book book)
    {
        cmd.Parameters.AddWithValue("@Title", book.Title);
        cmd.Parameters.AddWithValue("@Author", book.Author);
        cmd.Parameters.AddWithValue("@PublishYear", (object?)book.PublishYear ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Publisher", (object?)book.Publisher ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Isbn", (object?)book.Isbn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Category", (object?)book.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Description", (object?)book.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Toc", (object?)book.TocXml ?? DBNull.Value);
    }

    /// <summary>Отображает строку результата запроса на объект Book.</summary>
    private static Book MapBook(SqlDataReader r) => new()
    {
        BookId = r.GetInt32(r.GetOrdinal("BookId")),
        Title = r.GetString(r.GetOrdinal("Title")),
        Author = r.GetString(r.GetOrdinal("Author")),
        PublishYear = r.IsDBNull(r.GetOrdinal("PublishYear")) ? null : r.GetInt32(r.GetOrdinal("PublishYear")),
        Publisher = r.IsDBNull(r.GetOrdinal("Publisher")) ? null : r.GetString(r.GetOrdinal("Publisher")),
        Isbn = r.IsDBNull(r.GetOrdinal("Isbn")) ? null : r.GetString(r.GetOrdinal("Isbn")),
        Category = r.IsDBNull(r.GetOrdinal("Category")) ? null : r.GetString(r.GetOrdinal("Category")),
        Description = r.IsDBNull(r.GetOrdinal("Description")) ? null : r.GetString(r.GetOrdinal("Description")),
        TocXml = r.IsDBNull(r.GetOrdinal("Toc")) ? null : r.GetString(r.GetOrdinal("Toc"))
    };
}
