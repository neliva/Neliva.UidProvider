# Neliva.UidProvider

Generate unique, time-ordered identifiers (UIDs).

- Variable length: **16–32 bytes** — a 48-bit big-endian timestamp plus cryptographically strong random bytes.
- **Lexicographically sortable** by creation time when encoded as hex or base32hex.
- Allocation-free `Span<byte>` API.

## Byte layout

| Bytes   | Contents                                                      |
| ------- | ------------------------------------------------------------- |
| `0..5`  | 48-bit timestamp (big-endian), milliseconds since Unix epoch. |
| `6..31` | Cryptographically strong random bytes.                        |

## Usage

```csharp
using Neliva;

Span<byte> id = stackalloc byte[16];

UidProvider.System.Fill(id);

string hexId = Convert.ToHexString(id);
```

For long-term, high-assurance identifiers, 26 bytes (48-bit timestamp + 160-bit random) is recommended.
