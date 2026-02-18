namespace Risiko3D.Runtime.Contracts
{
    public static class ContractHandshake
    {
        public static bool IsClientCompatible(
            string hostContractVersion,
            string clientMinVersion,
            string clientMaxVersion,
            out string error)
        {
            error = string.Empty;
            if (!SemanticVersion.TryParse(hostContractVersion, out var host))
            {
                error = $"Invalid host contract version '{hostContractVersion}'.";
                return false;
            }

            if (!SemanticVersion.TryParse(clientMinVersion, out var min))
            {
                error = $"Invalid client min contract version '{clientMinVersion}'.";
                return false;
            }

            if (!SemanticVersion.TryParse(clientMaxVersion, out var max))
            {
                error = $"Invalid client max contract version '{clientMaxVersion}'.";
                return false;
            }

            if (min.CompareTo(max) > 0)
            {
                error = $"Invalid client contract range {clientMinVersion}..{clientMaxVersion}.";
                return false;
            }

            if (host.CompareTo(min) < 0 || host.CompareTo(max) > 0)
            {
                error = $"Host contract {hostContractVersion} is outside supported client range {clientMinVersion}..{clientMaxVersion}.";
                return false;
            }

            return true;
        }
    }
}

