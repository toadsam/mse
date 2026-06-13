package com.lastround.backend.repository;

import com.lastround.backend.entity.MatchPlayerStat;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.Collection;
import java.util.List;

// JPA repository for the match_player_stats table.
public interface MatchPlayerStatRepository extends JpaRepository<MatchPlayerStat, Long> {

    // Batch-loads player stats for a set of match IDs with user join-fetch to avoid N+1 queries.
    @Query("select stat from MatchPlayerStat stat join fetch stat.user where stat.match.id in :matchIds order by stat.match.id desc, stat.user.id asc")
    List<MatchPlayerStat> findAllByMatchIds(@Param("matchIds") Collection<Long> matchIds);
}