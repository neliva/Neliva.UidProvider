## Neliva.UidProvider

This repository provides functionality for generating unique, time-ordered identifiers. The specification and reference implementation are released into the public domain. See the [UNLICENSE](UNLICENSE.md) file.

[![main](https://github.com/neliva/Neliva.UidProvider/actions/workflows/main.yml/badge.svg)](https://github.com/neliva/Neliva.UidProvider/actions/workflows/main.yml)
[![dotnet 8.0](https://img.shields.io/badge/dotnet-8.0-green)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Neliva.UidProvider)](https://www.nuget.org/packages/Neliva.UidProvider)

## Overview

The `UidProvider` generates variable-size IDs ranging from 16 to 32 bytes. The identifiers are lexicographically sortable by time when encoded in hex or base32hex format.

Byte layout:

* Bytes 0..5  : 48-bit timestamp (big-endian), milliseconds since Unix epoch.
* Bytes 6..31 : cryptographically strong random bytes.

### Usage
```C#
// using Neliva;

var data = new byte[16]; // min ID size
var provider = new UidProvider();

provider.Fill(data);

// Using the built-in system provider

Span<byte> dataSpan = stackalloc byte[32]; // max ID size

UidProvider.System.Fill(dataSpan);
```

For global-scale, long-term, high-assurance document identification, it is recommended to generate 26-byte IDs (`48 bits timestamp + 160 bits random`). This is a balanced choice for **legal, forensic, and archival** use cases where an extremely low collision risk over multi-decade retention is paramount.