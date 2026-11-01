using ImmoralityGaming.Localization;

namespace Assets.Scripts.Combat
{
    /// <summary>A damage type's player-facing name. <see cref="DamageType.Normal"/> reads as "Physical".</summary>
    public static class DamageTypeNames
    {
        public static string Of(DamageType type)
        {
            switch (type)
            {
                case DamageType.Normal:
                    return Loc.Get("bestiary.element.physical");
                case DamageType.Fire:
                    return Loc.Get("bestiary.element.fire");
                case DamageType.Ice:
                    return Loc.Get("bestiary.element.ice");
                case DamageType.Lightning:
                    return Loc.Get("bestiary.element.lightning");
                case DamageType.Holy:
                    return Loc.Get("bestiary.element.holy");
                case DamageType.Shadow:
                    return Loc.Get("bestiary.element.shadow");
                default:
                    return type.ToString();
            }
        }
    }
}
