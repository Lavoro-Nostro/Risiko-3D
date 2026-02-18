using System;

namespace Risiko3D.Runtime.Contracts
{
    public readonly struct SemanticVersion : IComparable<SemanticVersion>
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }

        public SemanticVersion(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public int CompareTo(SemanticVersion other)
        {
            if (Major != other.Major) return Major.CompareTo(other.Major);
            if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
            return Patch.CompareTo(other.Patch);
        }

        public override string ToString()
        {
            return $"{Major}.{Minor}.{Patch}";
        }

        public static bool TryParse(string value, out SemanticVersion version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var parts = value.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            if (!int.TryParse(parts[0], out var major) ||
                !int.TryParse(parts[1], out var minor) ||
                !int.TryParse(parts[2], out var patch))
            {
                return false;
            }

            if (major < 0 || minor < 0 || patch < 0)
            {
                return false;
            }

            version = new SemanticVersion(major, minor, patch);
            return true;
        }
    }
}

