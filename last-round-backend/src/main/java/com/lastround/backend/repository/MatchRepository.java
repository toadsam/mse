// file: last-round-backend/src/main/java/com/lastround/backend/repository/MatchRepository.java
package com.lastround.backend.repository;

import com.lastround.backend.entity.Match;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

// JPA repository for the matches table with custom JPQL queries for user-scoped lookups.
public interface MatchRepository extends JpaRepository<Match, Long> {

    // Fetches all matches where the user appeared as either player, paginated by createdAt desc.
    @Query("select m from Match m where m.player1Id = :userId or m.player2Id = :userId")
    Page<Match> findByUserId(@Param("userId") Long userId, Pageable pageable);

    // Returns the number of matches won by the given user; used by the leaderboard.
    long countByWinnerId(Long winnerId);

    // Counts total matches played by a user (regardless of side) for win-rate calculation.
    @Query("select count(m) from Match m where m.player1Id = :userId or m.player2Id = :userId")
    long countTotalByUserId(@Param("userId") Long userId);
}
