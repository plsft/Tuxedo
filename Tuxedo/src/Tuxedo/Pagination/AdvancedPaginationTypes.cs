using System;
using System.Collections.Generic;
using System.Linq;
using Tuxedo.Patterns;

namespace Tuxedo.Pagination
{
    /// <summary>
    /// Result for cursor-based pagination
    /// </summary>
    public class CursorPagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public string? NextCursor { get; set; }
        public string? PreviousCursor { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
        public int Count => Items.Count;

        public CursorPagedResult()
        {
        }

        public CursorPagedResult(
            IEnumerable<T> items, 
            string? nextCursor = null, 
            string? previousCursor = null,
            bool hasNextPage = false,
            bool hasPreviousPage = false)
        {
            Items = items?.ToList() ?? new List<T>();
            NextCursor = nextCursor;
            PreviousCursor = previousCursor;
            HasNextPage = hasNextPage;
            HasPreviousPage = hasPreviousPage;
        }
    }

    /// <summary>
    /// DataTable request for server-side pagination
    /// </summary>
    public class DataTableRequest
    {
        public int Draw { get; set; }
        public int Start { get; set; }
        public int Length { get; set; }
        public List<DataTableColumn> Columns { get; set; } = new();
        public List<DataTableOrder> Order { get; set; } = new();
        public DataTableSearch Search { get; set; } = new();
        
        public int PageIndex => Length > 0 ? Start / Length : 0;
        public int PageSize => Length;
    }

    /// <summary>
    /// DataTable response for server-side pagination
    /// </summary>
    public class DataTableResponse<T>
    {
        public int Draw { get; set; }
        public int RecordsTotal { get; set; }
        public int RecordsFiltered { get; set; }
        public List<T> Data { get; set; } = new();
        public string? Error { get; set; }

        public DataTableResponse()
        {
        }

        public DataTableResponse(
            int draw,
            IEnumerable<T> data,
            int recordsTotal,
            int recordsFiltered)
        {
            Draw = draw;
            Data = data?.ToList() ?? new List<T>();
            RecordsTotal = recordsTotal;
            RecordsFiltered = recordsFiltered;
        }
    }

    /// <summary>
    /// DataTable column information
    /// </summary>
    public class DataTableColumn
    {
        public string Data { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Searchable { get; set; } = true;
        public bool Orderable { get; set; } = true;
        public DataTableSearch Search { get; set; } = new();
    }

    /// <summary>
    /// DataTable order information
    /// </summary>
    public class DataTableOrder
    {
        public int Column { get; set; }
        public string Dir { get; set; } = "asc";
        
        public bool IsAscending => Dir?.ToLowerInvariant() == "asc";
        public bool IsDescending => !IsAscending;
    }

    /// <summary>
    /// DataTable search information
    /// </summary>
    public class DataTableSearch
    {
        public string Value { get; set; } = string.Empty;
        public bool Regex { get; set; }
        
        public bool HasValue => !string.IsNullOrWhiteSpace(Value);
    }

    /// <summary>
    /// Keyset pagination result
    /// </summary>
    public class KeysetPagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public object? FirstKey { get; set; }
        public object? LastKey { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
        public int Count => Items.Count;

        public KeysetPagedResult()
        {
        }

        public KeysetPagedResult(
            IEnumerable<T> items,
            object? firstKey = null,
            object? lastKey = null,
            bool hasNextPage = false,
            bool hasPreviousPage = false)
        {
            Items = items?.ToList() ?? new List<T>();
            FirstKey = firstKey;
            LastKey = lastKey;
            HasNextPage = hasNextPage;
            HasPreviousPage = hasPreviousPage;
        }
    }

    /// <summary>
    /// Extended paged result with additional statistics
    /// </summary>
    public class PagedResultWithStats<T> : PagedResult<T>
    {
        public Dictionary<string, object> Statistics { get; set; } = new();
        public TimeSpan? QueryDuration { get; set; }
        public string? CacheKey { get; set; }
        public bool WasCached { get; set; }

        public PagedResultWithStats(
            IReadOnlyList<T> items,
            int pageIndex,
            int pageSize,
            int totalCount)
            : base(items, pageIndex, pageSize, totalCount)
        {
        }

        public void AddStatistic(string key, object value)
        {
            Statistics[key] = value;
        }
    }


    /// <summary>
    /// Extension methods for pagination validation
    /// </summary>
    public static class PaginationValidationExtensions
    {
        private const int MIN_PAGE_SIZE = 1;
        
        public static DataTableRequest Validate(this DataTableRequest request, PaginationOptions? options = null)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            
            options ??= new PaginationOptions();

            // Validate start
            if (request.Start < 0)
                request.Start = 0;

            // Validate length
            if (request.Length < MIN_PAGE_SIZE)
                request.Length = MIN_PAGE_SIZE;
            
            if (request.Length > options.MaxPageSize)
                request.Length = options.MaxPageSize;

            // Set default if not specified
            if (request.Length == 0)
                request.Length = options.DefaultPageSize;

            return request;
        }
    }

    /// <summary>
    /// Cursor encoder/decoder for security
    /// </summary>
    public static class CursorEncoder
    {
        public static string Encode(object value)
        {
            if (value == null)
                return string.Empty;

            var json = System.Text.Json.JsonSerializer.Serialize(value);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return Convert.ToBase64String(bytes);
        }

        public static T? Decode<T>(string? cursor)
        {
            if (string.IsNullOrWhiteSpace(cursor))
                return default;

            try
            {
                var bytes = Convert.FromBase64String(cursor);
                var json = System.Text.Encoding.UTF8.GetString(bytes);
                return System.Text.Json.JsonSerializer.Deserialize<T>(json);
            }
            catch
            {
                return default;
            }
        }
    }

    /// <summary>
    /// Sorting direction enum
    /// </summary>
    public enum SortDirection
    {
        Ascending,
        Descending
    }

    /// <summary>
    /// Sort descriptor for dynamic sorting
    /// </summary>
    public class SortDescriptor
    {
        public string PropertyName { get; set; } = string.Empty;
        public SortDirection Direction { get; set; } = SortDirection.Ascending;
        
        public bool IsAscending => Direction == SortDirection.Ascending;
        public bool IsDescending => Direction == SortDirection.Descending;

        public SortDescriptor()
        {
        }

        public SortDescriptor(string propertyName, SortDirection direction = SortDirection.Ascending)
        {
            PropertyName = propertyName;
            Direction = direction;
        }

        public string ToSql()
        {
            return $"{PropertyName} {(IsAscending ? "ASC" : "DESC")}";
        }
    }

    /// <summary>
    /// Extended page request with sorting and filtering
    /// </summary>
    public class AdvancedPageRequest : PageRequest
    {
        public List<SortDescriptor> SortDescriptors { get; set; } = new();
        public new Dictionary<string, object> Filters { get; set; } = new();
        public string? SearchTerm { get; set; }
        public bool IncludeTotalCount { get; set; } = true;

        public void AddSort(string propertyName, SortDirection direction = SortDirection.Ascending)
        {
            SortDescriptors.Add(new SortDescriptor(propertyName, direction));
        }

        public void AddFilter(string key, object value)
        {
            Filters[key] = value;
        }

        public string GetOrderBySql()
        {
            if (!SortDescriptors.Any())
                return string.Empty;

            var orderClauses = SortDescriptors.Select(s => s.ToSql());
            return $"ORDER BY {string.Join(", ", orderClauses)}";
        }
    }
}