## Neliva.UidProvider

This repository provides functionality for generating unique across space and time identifiers.

[![main](https://github.com/neliva/Neliva.UidProvider/actions/workflows/main.yml/badge.svg)](https://github.com/neliva/Neliva.UidProvider/actions/workflows/main.yml)
[![dotnet 6.0](https://img.shields.io/badge/dotnet-6.0-green)](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Neliva.UidProvider)](https://www.nuget.org/packages/Neliva.UidProvider)

## Overview

The UidProvider generates variable size IDs ranging from 16 bytes to 32 bytes.

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
