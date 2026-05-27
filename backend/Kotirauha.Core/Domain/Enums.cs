namespace Kotirauha.Core.Domain;

public enum MembershipRole
{
    Resident = 0,
    Board = 1,
    Admin = 2,
}

public enum IncidentCategory
{
    Noise = 0,
    Smell = 1,
    SmokingOrIncense = 2,
    Parking = 3,
    SafetyConcern = 4,
    CommonAreaMisuse = 5,
    Other = 6,
}

public enum TranslationStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
}
