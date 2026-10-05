using Manifest.Models;

namespace Manifest.Data;

public interface ICardRepository
{
    CatalogRow? Resolve(string cardId);
    List<SearchRow> Search(BinderScope scope, string? q, string? limit, bool ownedOnly,
                           string? category, string? color, string? rarity,
                           string? setLabel);
    Page<SearchRow> SearchPage(BinderScope scope, CardFilter filter, string? sort, int limit,
                               string? cursor);
    Page<CollectionRow> CollectionPage(BinderScope scope, CardFilter filter, string? sort,
                                       int limit, string? cursor);
    Facets Facets();
    List<CollectionRow> Collection(BinderScope scope);
    CardDetailRow? CardDetail(BinderScope scope, string rawCardId);
    List<SearchRow> Prints(BinderScope scope, string rawCardId);
    Stats Stats(BinderScope scope);
    AdjustResult Adjust(long binderId, long actorId, string rawCardId, int? delta, int? qty,
                        string? note);
    BulkLogResult AdjustMany(long binderId, long actorId, IReadOnlyList<CardQuantity> cards,
                             string? note);
    void ResetCollection(long binderId);
    long CatalogCount();
}

public interface IUserRepository
{
    long Count();
    User? ById(long id);
    User? ByName(string username);
    List<User> List();
    User Create(string username, string password, string? email = null);
    User? Authenticate(string? username, string? password);
    void SetPassword(long userId, string password);
    bool Delete(string username);
}

public interface ISessionRepository
{
    TimeSpan SessionLifetime { get; }
    string StartSession(long userId);
    User? ForToken(string? token);
    void EndSession(string? token);
    int PurgeExpiredSessions();
}

public interface IAccessRepository
{
    AccessRepository.Submission Submit(string email, string? note, string? from);
    AccessRequest? ById(long id);
    AccessRequest? ByActionToken(string? token);
    (AccessRequest Request, string InviteToken)? Approve(long id, string decidedBy);
    AccessRequest? Deny(long id, string decidedBy);
    AccessRequest? ByInviteToken(string? token);
    bool Spend(string token);
    void Attach(long id, long userId);
    void Unspend(long id, string token);
    List<AccessRequest> List(string? status = null, int limit = 200);
    long PendingCount();
    int PurgeExpiredTokens();
}

public interface IDeckRepository
{
    List<DeckListItem> List(long userId);
    Page<DeckListItem> ListPage(long userId, int limit, string? cursor);
    DeckDetail? Detail(long userId, long deckId);
    DeckDetail Create(long userId, string? name, string leaderCardId);
    DeckDetail? Update(long userId, long deckId, string? name, string? leaderCardId);
    void Delete(long userId, long deckId);
    DeckDetail? SetCard(long userId, long deckId, string cardId, int? qty);
    DeckDetail? SetCards(long userId, long deckId, IReadOnlyList<CardQuantity> cards);
    DeckDetail? SwapPrint(long userId, long deckId, string fromCardId, string toCardId);
}

public interface IScanRepository
{
    void LogScan(long userId, string? cardId, string result);
}
