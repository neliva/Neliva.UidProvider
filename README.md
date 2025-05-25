## Neliva.UidProvider

This repository provides functionality for generating unique across space and time identifiers. The specification and the reference implementation are released into the public domain. See the [UNLICENSE](UNLICENSE.md) file.

[![main](https://github.com/neliva/Neliva.UidProvider/actions/workflows/main.yml/badge.svg)](https://github.com/neliva/Neliva.UidProvider/actions/workflows/main.yml)
[![dotnet 6.0](https://img.shields.io/badge/dotnet-8.0-green)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Neliva.UidProvider)](https://www.nuget.org/packages/Neliva.UidProvider)

## Overview

The UidProvider generates variable size IDs ranging from 16 bytes to 32 bytes. An ID consists of:

* A 6-byte timestamp, representing the ID's creation, measured in milliseconds since the Unix epoch.
* A 6-byte random value generated once per provider instance.
* A 4-byte incrementing counter, initialized to a random value per provider instance.
* A random value if the ID length is greater than 16 bytes.

For the timestamp and counter values, the most significant bytes appear first in the byte sequence (big-endian). The IDs are lexicographically sortable when encoded in hex or base32hex format.

The byte format of the ID is the following:
```
+-------------+--------+-----------+----------+
|  Timestamp  |  Node  |  Counter  |  Random  |
+-------------+--------+-----------+----------+
|  6          |  6     |  4        |  0 - 16  |
+-------------+--------+-----------+----------+ 
```

### Usage
```C#
// using Neliva;

var data = new byte[16]; // min ID size
var provider = new UidProvider(/* node span, timestamp callback, RNG callback */);

provider.Fill(data);

// Using the default provider

Span<byte> dataSpan = stackalloc byte[32]; // max ID size

UidProvider.Default.Fill(dataSpan);
```
