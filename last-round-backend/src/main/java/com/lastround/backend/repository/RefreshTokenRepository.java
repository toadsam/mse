// file: last-round-backend/src/main/java/com/lastround/backend/repository/RefreshTokenRepository.java
package com.lastround.backend.repository;

import com.lastround.backend.entity.RefreshToken;
import com.lastround.backend.entity.User;
import org.springframework.data.jpa.repository.JpaRepository;

import java.time.LocalDateTime;
import java.util.Optional;

public interface RefreshTokenRepository extends JpaRepository<RefreshToken, Long> {
    Optional<RefreshToken> findByToken(String token);

    void deleteByUser(User user);

    void deleteByExpiresAtBefore(LocalDateTime dateTime);
}
