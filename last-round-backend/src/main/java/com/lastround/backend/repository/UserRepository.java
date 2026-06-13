// file: last-round-backend/src/main/java/com/lastround/backend/repository/UserRepository.java
package com.lastround.backend.repository;

import com.lastround.backend.entity.User;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.Optional;

// JPA repository for the users table.
public interface UserRepository extends JpaRepository<User, Long> {
    // Used during login to load the user by credential email.
    Optional<User> findByEmail(String email);

    // Used during signup to enforce unique email before attempting an insert.
    boolean existsByEmail(String email);

    // Used during signup to enforce unique nickname before attempting an insert.
    boolean existsByNickname(String nickname);
}
