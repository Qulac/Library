Клиент разработан на .Net 8.0, целевая платформа - windows, тип проекта - Приложение windows,UI - Windows-Forms
Сервер базы данных - Ms Sql Server упакован в контейнер. Для контейнера используется образ mcr.microsoft.com/mssql/server:2022-latest
Бизнес логика согласно заданию выполнена в бд в виде хранимок, валидация на треггерах.

Запуск.
Для запуска приложения необходимо что бы не целевом компьютере был установлен docker desktop.
1. Откройте cmd передите в папку Library
2. Выполните сборку образа контейнера:
docker build -t homelibrary-mssql .  (точка на конце обязательна)
3. Запустите контейнер:
docker run -d --name homelibrary-mssql -p 1433:1433 -v mssql-data:/var/opt/mssql homelibrary-mssql
4. Запустите клиент:
cd src\HomeLibrary
dotnet run
