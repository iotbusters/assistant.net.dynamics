# Assistant.NET.Dynamics Changelog

All relevant changes to packages which were released or being prepared for releasing.

See also [keepachangelog.com](https://keepachangelog.com/en/1.0.0/).

## Unreleased

### Unreleased Added

- opt-in runtime proxy generation package, isolating the Roslyn compiler dependency from the default runtime package
- support for intercepting property setters
- support for `ref`/`out`/`in` method parameters, including write-back after interception
- compile-time proxy generation trigger via a `[Proxy]` attribute and an assembly-level attribute, in addition to usage-based detection
- benchmarks project for measuring proxy call overhead
- automated tests exercising the source generator end-to-end
- a thread-safety test covering concurrent proxy creation and interceptor registration
- vulnerable-dependency scanning in CI

### Unreleased Changed

- retargeted all packages to modern, currently supported .NET target frameworks
- upgraded all dependencies to their latest supported versions
- rewrote the source generator on the current incremental generator API for better performance and diagnostics
- reworked the interception pipeline to avoid per-call allocations and rebuilding on every invocation
- made the proxy registry thread-safe and removed reflection-based instantiation
- split runtime abstractions into a dedicated, lightweight package
- upgraded CI/CD pipelines to the current .NET SDK

### Unreleased Fixed

- interceptors configured for property setters now actually receive the assigned value
- a startup configuration option that previously rejected a valid, more permissive generation strategy
- reflection lookups that silently failed for methods with `ref`/`out` parameters
- generation failures are now reported with actionable diagnostics instead of being swallowed

## 0.0.11 - 2021-08-17

[Assistant.NET.Diagnostics Release 0.0.11](https://github.com/iotbusters/assistant.net.dynamics/releases/tag/0.0.11)

### 0.0.11 Added

- initial implementation
