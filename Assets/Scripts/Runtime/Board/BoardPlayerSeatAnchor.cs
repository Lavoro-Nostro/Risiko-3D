using UnityEngine;
using System.Text.RegularExpressions;

namespace Risiko3D.Runtime.Board
{
    public sealed class BoardPlayerSeatAnchor : MonoBehaviour
    {
        [Range(1, 6)]
        [SerializeField] private int seatNumber = 1;
        [SerializeField] private Transform cameraAnchor;
        [SerializeField] private Transform cardsAnchor;
        [SerializeField] private bool useSelfWhenAnchorMissing = true;
        [SerializeField] private bool autoSeatNumberFromObjectName = true;

        public int SeatNumber
        {
            get
            {
                if (autoSeatNumberFromObjectName && TryParseSeatNumberFromName(gameObject != null ? gameObject.name : string.Empty, out var parsed))
                {
                    return parsed;
                }

                return Mathf.Clamp(seatNumber, 1, 6);
            }
        }

        public Transform ResolveCameraAnchor()
        {
            if (cameraAnchor != null)
            {
                return cameraAnchor;
            }

            return useSelfWhenAnchorMissing ? transform : null;
        }

        public Transform ResolveCardsAnchor()
        {
            if (cardsAnchor != null)
            {
                return cardsAnchor;
            }

            return useSelfWhenAnchorMissing ? transform : null;
        }

        private void OnValidate()
        {
            seatNumber = Mathf.Clamp(seatNumber, 1, 6);
            if (autoSeatNumberFromObjectName && TryParseSeatNumberFromName(gameObject != null ? gameObject.name : string.Empty, out var parsed))
            {
                seatNumber = parsed;
            }
        }

        private static bool TryParseSeatNumberFromName(string objectName, out int number)
        {
            number = 0;
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            // Accept common naming patterns such as "Seat_1", "Seat1", "seat-2".
            var match = Regex.Match(objectName, @"(?i)\bseat[\s_\-]*(\d+)\b");
            if (!match.Success || match.Groups.Count < 2)
            {
                return false;
            }

            if (!int.TryParse(match.Groups[1].Value, out var parsed))
            {
                return false;
            }

            if (parsed < 1 || parsed > 6)
            {
                return false;
            }

            number = parsed;
            return true;
        }
    }
}
