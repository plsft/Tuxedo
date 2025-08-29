# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.0] - 2025-08-28

### Added
- **Query Caching**: Intelligent caching with tag-based invalidation
  - `IQueryCache` interface with memory-based implementation
  - Extension methods `QueryWithCacheAsync` and `QuerySingleWithCacheAsync`
  - Automatic cache key generation using SHA256
  - Thread-safe operations with `SemaphoreSlim`

- **Advanced Query Builder**: Fluent LINQ-style query building
  - Expression support for type-safe queries
  - Complex JOIN operations (INNER, LEFT, RIGHT)
  - Subquery support (EXISTS, NOT EXISTS)
  - Set operations (UNION, INTERSECT, EXCEPT)
  - Aggregate functions with GROUP BY/HAVING
  - Pagination with Skip/Take

- **Bulk Operations**: High-performance batch operations
  - `BulkInsert`, `BulkUpdate`, `BulkDelete`, `BulkMerge`
  - Optimized for each database dialect
  - Transaction support with configurable batch sizes
  - Column mapping and exclusion options

- **Resiliency & Circuit Breakers**: Polly integration
  - Automatic retry with exponential backoff
  - Circuit breaker pattern implementation
  - Configurable retry policies
  - Timeout handling

- **Repository Pattern**: Generic repository with specifications
  - `IRepository<T>` interface with full CRUD operations
  - Specification pattern for complex queries
  - Async support throughout

- **Unit of Work Pattern**: Transaction management
  - Automatic change tracking
  - Deferred execution
  - Multiple repository coordination

- **Comprehensive Test Coverage**
  - 300+ unit tests for all features
  - Integration tests for all database providers
  - 93%+ test pass rate

### Fixed
- Resolved all Dapper reference issues
- Fixed partial update method signatures
- Corrected namespace conflicts in QueryBuilder
- Fixed memory cache initialization issues

### Note
All enterprise features are included in the open-source release without licensing restrictions.

## [0.2.0] - 2025-08-26

### Added
- SQL-aligned `Select` method aliases for all `Query` and `Get` operations
- `Select<T>()` as an alias for `Query<T>()` with full parameter support
- `Select<T>(id)` as an alias for `Get<T>(id)` for selecting by primary key
- `SelectAll<T>()` as an alias for `GetAll<T>()` for selecting all records
- `SelectFirst<T>()`, `SelectFirstOrDefault<T>()`, `SelectSingle<T>()`, `SelectSingleOrDefault<T>()` aliases
- Multi-mapping `Select` variants supporting 2-7 type parameters
- Async versions of all `Select` methods (`SelectAsync`, `SelectAllAsync`, etc.)
- Complete SQL verb alignment: `Select`, `Insert`, `Update`, `Delete`

### Changed
- Updated README with SQL-aligned API documentation and examples
- Enhanced feature list to highlight the dual API support

### Technical Details
- Added Select method aliases in `SqlMapper.cs` for core query operations
- Added SelectAsync method aliases in `SqlMapper.Async.cs` for async operations
- Added Select/SelectAll aliases in `SqlMapperExtensions.cs` for Contrib methods
- Added SelectAsync/SelectAllAsync aliases in `SqlMapperExtensions.Async.cs`
- All aliases maintain exact parameter compatibility with original methods

## [0.1.0] - 2025-08-25

### Added
- Initial release of Tuxedo
- Merged Dapper and Dapper.Contrib into a single unified package
- Multi-targeting support for .NET 6.0, .NET 8.0, and .NET 9.0
- Built-in database adapters for SQL Server, PostgreSQL, and MySQL
- Modernized codebase with nullable reference types
- Apache 2.0 license preserving original attribution