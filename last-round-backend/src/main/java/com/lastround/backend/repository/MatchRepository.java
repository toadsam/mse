// file: last-round-backend/src/main/java/com/lastround/backend/repository/MatchRepository.java
package com.lastround.backend.repository;

import com.lastround.backend.entity.Match;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface MatchRepository extends JpaRepository<Match, Long> {

    @Query("select m from Match m where m.player1Id = :userId or m.player2Id = :userId")
    Page<Match> findByUserId(@Param("userId") Long userId, Pageable pageable);

    long countByWinnerId(Long winnerId);

    @Query("select count(m) from Match m where m.player1Id = :userId or m.player2Id = :userId")
    long countTotalByUserId(@Param("userId") Long userId);
}
