# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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