# ============================================================
# Dockerfile сервера БД приложения "Домашняя библиотека".
#
# SQL Server 2022 + автоматическая инициализация БД при первом старте.
# Используется штатный механизм образа: скрипты из каталога
# /mssql-server-setup-scripts.d выполняются автоматически после того,
# как SQL Server стал готов (только при первой инициализации — пока
# каталог данных /var/opt/mssql/data пуст; при последующих стартах
# с существующими данными инициализация пропускается).
#
# Сборка:  docker build -t homelibrary-mssql .
# Запуск:  docker run -d --name homelibrary-mssql -p 1433:1433 -v mssql-data:/var/opt/mssql homelibrary-mssql
# ============================================================

FROM mcr.microsoft.com/mssql/server:2022-latest

# Лицензия и пароль sa по умолчанию (можно переопределить ключом -e при запуске)
ENV ACCEPT_EULA=Y \
    MSSQL_SA_PASSWORD=YourStrong!Pass1

# Идемпотентный скрипт инициализации: БД HomeLibrary, таблица Books,
# индексы (включая XML) и хранимые процедуры insert/update/delete/select/search
COPY db/init.sql /mssql-server-setup-scripts.d/init.sql

# Файл мог быть создан в Windows — убираем CRLF
USER root
RUN sed -i 's/\r$//' /mssql-server-setup-scripts.d/init.sql
USER mssql

# Контейнер считается здоровым, когда SQL Server отвечает на запросы
HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=10 \
    CMD /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1" -b -o /dev/null || exit 1

# Точка входа не переопределяется: используется штатная образа
# (/opt/mssql/bin/launch_sqlservr.sh) — она сама выполняет скрипты
# из /mssql-server-setup-scripts.d при первой инициализации.
