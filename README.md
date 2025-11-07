## Neliva.UidProvider

This repository provides functionality for generating unique, time-ordered identifiers. Both the specification and the reference implementation are released into the public domain. See the [UNLICENSE](UNLICENSE.md) file for details.


[![main](https://github.com/neliva/Neliva.UidProvider/actions/workflows/main.yml/badge.svg)](https://github.com/neliva/Neliva.UidProvider/actions/workflows/main.yml)
[![.NET 8.0](https://img.shields.io/badge/dotnet-8.0-green)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![NuGet (with prereleases)](https://img.shields.io/nuget/vpre/Neliva.UidProvider)](https://www.nuget.org/packages/Neliva.UidProvider)

## Overview

The `UidProvider` generates variable-length IDs ranging from 16 to 32 bytes. These identifiers are lexicographically sortable by time when encoded in hexadecimal or base32hex format.

### Byte layout

* Bytes `0..5`  : 48-bit timestamp (big-endian), milliseconds since Unix epoch.
* Bytes `6..31` : Cryptographically strong random bytes.

## Usage

```C#
// using Neliva;

var data = new byte[16]; // min ID size
var provider = new UidProvider();

provider.Fill(data);

// Using the built-in system provider

Span<byte> dataSpan = stackalloc byte[32]; // max ID size

UidProvider.System.Fill(dataSpan);
```

Recommended ID size for long-term, high-assurance document identification is 26 bytes (48 bits timestamp + 160 bits random). This offers an extremely low collision probability over multi-decade retention for legal, forensic, and archival scenarios.
