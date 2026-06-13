package com.lastround.backend.repository;

import com.lastround.backend.entity.MatchPlayerAugment;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.Collection;
import java.util.List;

// JPA repository for the match_player_augments table.
public interface MatchPlayerAugmentRepository extends JpaRepository<MatchPlayerAugment, Long> {

    // Batch-loads augment picks for a set of match IDs with left-join on the Augment FK
    // (nullable FK — left join ensures rows are returned even when augmentId is null).
    @Query("select augment from MatchPlayerAugment augment left join fetch augment.augment where augment.match.id in :matchIds order by augment.match.id desc, augment.user.id asc, augment.selectedRound asc, augment.selectedOrder asc")
    List<MatchPlayerAugment> findAllByMatchIds(@Param("matchIds") Collection<Long> matchIds);
}