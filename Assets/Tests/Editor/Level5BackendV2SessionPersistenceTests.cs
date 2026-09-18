using System;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// <see cref="BackendV2SessionPersistenceStore"/>: the plaintext-JSON, <c>AtomicFile</c>-backed
    /// refresh-token persistence resolving the security decision <c>BackendV2SessionStore.cs</c>
    /// used to leave open.
    /// </summary>
    public class Level5BackendV2SessionPersistenceTests
    {
        [TearDown]
        public void TearDown()
        {
            BackendV2SessionPersistenceStore.Clear();
        }

        private static BackendV2Session Session(
            DateTimeOffset? expiresAt = null, DateTimeOffset? refreshExpiresAt = null)
        {
            return new BackendV2Session(
                "access-token-value",
                expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(10),
                Guid.NewGuid(),
                "refresh-token-value",
                refreshExpiresAt ?? DateTimeOffset.UtcNow.AddDays(30));
        }

        [Test]
        public void NothingIsPersistedByDefault()
        {
            Assert.That(BackendV2SessionPersistenceStore.TryLoad(out _), Is.False);
        }

        [Test]
        public void ASavedSessionRoundTrips()
        {
            BackendV2Session saved = Session();

            BackendV2SessionPersistenceStore.Save(saved);
            bool loaded = BackendV2SessionPersistenceStore.TryLoad(out BackendV2Session restored);

            Assert.That(loaded, Is.True);
            Assert.That(restored.PlayerId, Is.EqualTo(saved.PlayerId));
            Assert.That(restored.RefreshToken, Is.EqualTo(saved.RefreshToken));
            Assert.That(restored.AccessToken, Is.EqualTo(saved.AccessToken));
            Assert.That(restored.ExpiresAt.ToUnixTimeSeconds(), Is.EqualTo(saved.ExpiresAt.ToUnixTimeSeconds()));
            Assert.That(
                restored.RefreshTokenExpiresAt.ToUnixTimeSeconds(),
                Is.EqualTo(saved.RefreshTokenExpiresAt.ToUnixTimeSeconds()));
        }

        [Test]
        public void SavingNullClearsAnyPersistedSession()
        {
            BackendV2SessionPersistenceStore.Save(Session());

            BackendV2SessionPersistenceStore.Save(null);

            Assert.That(BackendV2SessionPersistenceStore.TryLoad(out _), Is.False);
        }

        [Test]
        public void ARefreshTokenAlreadyPastItsOwnExpiryIsNotHandedBack()
        {
            BackendV2SessionPersistenceStore.Save(Session(
                refreshExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-1)));

            bool loaded = BackendV2SessionPersistenceStore.TryLoad(out BackendV2Session restored);

            Assert.That(loaded, Is.False, "a session that can only fail a refresh must not be handed back");
            Assert.That(restored, Is.Null);
        }

        [Test]
        public void ASemanticallyInvalidSessionIsClearedNotLeftToFailForever()
        {
            // Structurally valid JSON (Save() always writes every field, so this needs a
            // deliberately incomplete session) but with no refresh token - same "can only ever fail
            // again" reasoning as an expired one, so it must self-heal by being cleared rather than
            // permanently blocking auto-restore with no way to recover.
            BackendV2SessionPersistenceStore.Save(new BackendV2Session(
                "access-token-value", DateTimeOffset.UtcNow.AddMinutes(10), Guid.NewGuid(),
                refreshToken: string.Empty, refreshTokenExpiresAt: DateTimeOffset.UtcNow.AddDays(30)));

            bool loaded = BackendV2SessionPersistenceStore.TryLoad(out _);

            Assert.That(loaded, Is.False);
            Assert.That(BackendV2SessionPersistenceStore.TryLoad(out _), Is.False,
                "a second load must not still find the invalid file, confirming it was cleared");
        }

        [Test]
        public void LoadingAfterClearFindsNothing()
        {
            BackendV2SessionPersistenceStore.Save(Session());
            BackendV2SessionPersistenceStore.Clear();

            Assert.That(BackendV2SessionPersistenceStore.TryLoad(out _), Is.False);
        }

        [Test]
        public void SessionStoreChangedEventTriggersDoNotThrowWithNoSubscribers()
        {
            // Set/Clear must remain safe to call even when nothing (e.g. the persistence bootstrap,
            // which only wires up at real process startup) is listening - which is the normal
            // situation for every other EditMode test in this suite.
            Assert.DoesNotThrow(() =>
            {
                BackendV2SessionStore.Set(Session());
                BackendV2SessionStore.Clear();
            });
        }
    }
}
