// file: last-round-backend/src/main/java/com/lastround/backend/repository/AugmentRepository.java
package com.lastround.backend.repository;

import com.lastround.backend.entity.Augment;
import org.springframework.data.jpa.repository.JpaRepository;

// JPA repository for the augments table. Standard CRUD is inherited from JpaRepository.
public interface AugmentRepository extends JpaRepository<Augment, Long> {
}
