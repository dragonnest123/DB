# AzotBase

AzotBase — учебный проект встраиваемой базы данных на C# и .NET. Сейчас репозиторий находится в активной разработке. README будет обновляться вместе с развитием API
## Что уже есть

- **Постраничное хранение данных**: `PageManager`, страницы данных, индексные и системные страницы, заголовки страниц и слоты записей.
- **B+ tree индекс**: вставка, поиск, удаление, split/merge и балансировка страниц индекса.
- **Кэш страниц**: LRU-кэш с pin/unpin-механикой и событием вытеснения для записи изменённых страниц на диск.
- **Сериализация объектов**: сериализация классов и структур, поддержка порядка полей через атрибуты.
- **Конкурентный доступ**: асинхронный reader-writer lock и адаптеры блокировок.
- **Начальный API базы**: создание, подключение и удаление таблиц через `AzotBase.Database.Api.AzotBase`.
- **Набор тестов**: xUnit-тесты для кэша, сериализации, блокировок, страниц и B+ tree.

## Пример API

```csharp
using AzotBase.Database.Api;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["DBSettings:DBDirectoryPath"] = "./data",
        ["DBSettings:DBName"] = "example-db"
    })
    .Build();

var db = new AzotBase.Database.Api.AzotBase(config);
db.CreateTable<User>("users");

var users = db.ConnectTable<User>("users");
```

## Конфигурация

База ожидает секцию `DBSettings`:

```json
{
  "DBSettings": {
    "DBDirectoryPath": "./data",
    "DBName": "AzotBase"
  }
}
```

- `DBDirectoryPath` — обязательный путь к директории, где будут создаваться файлы базы.
- `DBName` — имя базы данных. Если значение не задано, будет использовано имя с автоматически сгенерированным суффиксом.

## Что планируется дальше

- Завершить CRUD-операции для `DBTable<T>`.
- Добавить полноценный поиск записей через B+ tree.
- Расширить тестовое покрытие интеграционными сценариями для API базы.
- Реализовать транзакции, WAL/журналирование и восстановление после аварийного завершения.
