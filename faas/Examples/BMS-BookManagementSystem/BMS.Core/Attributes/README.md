# Global Copyright Attribute

## Overview
The `CopyrightAttribute` has been moved from `BMS-API/Attributes/` to `BMS.Core/Attributes/` to make it globally available across all projects in the solution.

## Location
- **Previous location**: `BMS-API/Attributes/CopyrightAttribute.cs`
- **New location**: `BMS.Core/Attributes/CopyrightAttribute.cs`
- **Namespace**: `BMS.Core.Attributes`

## Usage

### 1. Add using statement
```csharp
using BMS.Core.Attributes;
```

### 2. Apply to classes or methods
```csharp
[Copyright("ARJUN A L", 2025)]
public class MyController : ControllerBase
{
    // Controller implementation
}
```

### 3. Get copyright information at runtime
```csharp
// Using the helper method
string copyrightInfo = CopyrightHelper.GetCopyright(typeof(MyController));
// Returns: "© 2025 ARJUN A L - Licensed under MIT License"
```

## Features
- **Author**: Specify the copyright holder
- **Year**: Copyright year
- **License**: Defaults to "MIT" license
- **Runtime access**: Use `CopyrightHelper.GetCopyright(Type)` to retrieve copyright info via reflection

## Projects with Access
Since `BMS.Core` is referenced by most projects in the solution, the attribute is available in:
- BMS-API
- BMS.Interface  
- BMS.External
- Any other project that references BMS.Core

## Example Implementation
See `BMS-API/Controllers/Artichoke/AuthController.cs` for a working example of the attribute in use.