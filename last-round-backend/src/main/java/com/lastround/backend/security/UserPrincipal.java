// file: last-round-backend/src/main/java/com/lastround/backend/security/UserPrincipal.java
package com.lastround.backend.security;

import com.lastround.backend.entity.User;
import lombok.Getter;
import org.springframework.security.core.GrantedAuthority;
import org.springframework.security.core.userdetails.UserDetails;

import java.util.Collection;
import java.util.List;

// Spring Security principal wrapping a User entity — placed in the SecurityContext after JWT validation.
@Getter
public class UserPrincipal implements UserDetails {
    private final Long id;
    private final String email;
    private final String password;

    public UserPrincipal(User user) {
        this.id = user.getId();
        this.email = user.getEmail();
        this.password = user.getPassword();
    }

    // No role-based access control is implemented; all authenticated users have the same access.
    @Override
    public Collection<? extends GrantedAuthority> getAuthorities() {
        return List.of();
    }

    @Override
    public String getPassword() {
        return password;
    }

    // Spring Security uses this as the "username" identifier; we use email.
    @Override
    public String getUsername() {
        return email;
    }

    @Override
    public boolean isAccountNonExpired() {
        return true;
    }

    @Override
    public boolean isAccountNonLocked() {
        return true;
    }

    @Override
    public boolean isCredentialsNonExpired() {
        return true;
    }

    @Override
    public boolean isEnabled() {
        return true;
    }
}
