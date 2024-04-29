## Neliva.UidProvider

This repository provides functionality for generating unique across space and time identifiers.

[![main](https://github.com/neliva/Neliva.UidProvider/actions/workflows/main.yml/badge.svg)](https://github.com/neliva/Neliva.UidProvider/actions/workflows/main.yml)
[![dotnet 6.0](https://img.shields.io/badge/dotnet-6.0-green)](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Neliva.UidProvider)](https://www.nuget.org/packages/Neliva.UidProvider)

## Overview

The UidProvider generates variable size IDs ranging from 16 bytes to 32 bytes and consists of:

* A 6-byte timestamp, representing the ID's creation, measured in milliseconds since the Unix epoch.
* A 6-byte random value generated once per process for the default provider instance.
* A 4-byte incrementing counter, initialized to a random value.
* A random value if the ID length is greater than 16 bytes.

For the timestamp and counter values, the most significant bytes appear first in the byte sequence (big-endian). The IDs are lexicographically sortable when encoded in hex or base32 format.

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
