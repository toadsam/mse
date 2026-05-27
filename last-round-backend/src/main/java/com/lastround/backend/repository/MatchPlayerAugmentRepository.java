package com.lastround.backend.repository;

import com.lastround.backend.entity.MatchPlayerAugment;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.Collection;
import java.util.List;

public interface MatchPlayerAugmentRepository extends JpaRepository<MatchPlayerAugment, Long> {

	@Query("select augment from MatchPlayerAugment augment join fetch augment.augment where augment.match.id in :matchIds order by augment.match.id desc, augment.user.id asc, augment.selectedRound asc, augment.selectedOrder asc")
	List<MatchPlayerAugment> findAllByMatchIds(@Param("matchIds") Collection<Long> matchIds);
}