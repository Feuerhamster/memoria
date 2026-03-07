namespace Memoria.Services.RadicaleClient;

public interface IRadicaleClient
{
    // --- Collection lifecycle ---
    Task CreateCalendarCollection(Guid spaceId, CancellationToken ct = default);
    Task CreateContactsCollection(Guid spaceId, CancellationToken ct = default);
    Task DeleteCalendarCollection(Guid spaceId, CancellationToken ct = default);
    Task DeleteContactsCollection(Guid spaceId, CancellationToken ct = default);

    // --- Calendar events ---
    Task<List<(Guid Id, string ETag)>> ListEventHrefs(Guid spaceId, CancellationToken ct = default);
    Task<string> GetEventIcs(Guid spaceId, Guid eventId, CancellationToken ct = default);
    Task PutEventIcs(Guid spaceId, Guid eventId, string ics, CancellationToken ct = default);
    Task DeleteEvent(Guid spaceId, Guid eventId, CancellationToken ct = default);

    // --- Contacts ---
    Task<List<(Guid Id, string ETag)>> ListContactHrefs(Guid spaceId, CancellationToken ct = default);
    Task<string> GetContactVcf(Guid spaceId, Guid contactId, CancellationToken ct = default);
    Task PutContactVcf(Guid spaceId, Guid contactId, string vcf, CancellationToken ct = default);
    Task DeleteContact(Guid spaceId, Guid contactId, CancellationToken ct = default);
}
