﻿// <auto-generated />
using System;
using MareSynchronosShared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MareSynchronosServer.Migrations
{
    [DbContext(typeof(MareDbContext))]
    [Migration("20230228001033_UserPerms")]
    partial class UserPerms
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.3")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("MareSynchronosShared.Models.Auth", b =>
                {
                    b.Property<string>("HashedKey")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("hashed_key");

                    b.Property<bool>("IsBanned")
                        .HasColumnType("boolean")
                        .HasColumnName("is_banned");

                    b.Property<string>("PrimaryUserUID")
                        .HasColumnType("character varying(10)")
                        .HasColumnName("primary_user_uid");

                    b.Property<string>("UserUID")
                        .HasColumnType("character varying(10)")
                        .HasColumnName("user_uid");

                    b.HasKey("HashedKey")
                        .HasName("pk_auth");

                    b.HasIndex("PrimaryUserUID")
                        .HasDatabaseName("ix_auth_primary_user_uid");

                    b.HasIndex("UserUID")
                        .HasDatabaseName("ix_auth_user_uid");

                    b.ToTable("auth", (string)null);
                });

            modelBuilder.Entity("MareSynchronosShared.Models.Banned", b =>
                {
                    b.Property<string>("CharacterIdentification")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("character_identification");

                    b.Property<string>("Reason")
                        .HasColumnType("text")
                        .HasColumnName("reason");

                    b.Property<byte[]>("Timestamp")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("bytea")
                        .HasColumnName("timestamp");

                    b.HasKey("CharacterIdentification")
                        .HasName("pk_banned_users");

                    b.ToTable("banned_users", (string)null);
                });

            modelBuilder.Entity("MareSynchronosShared.Models.BannedRegistrations", b =>
                {
                    b.Property<string>("DiscordIdOrLodestoneAuth")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("discord_id_or_lodestone_auth");

                    b.HasKey("DiscordIdOrLodestoneAuth")
                        .HasName("pk_banned_registrations");

                    b.ToTable("banned_registrations", (string)null);
                });

            modelBuilder.Entity("MareSynchronosShared.Models.ClientPair", b =>
                {
                    b.Property<string>("UserUID")
                        .HasMaxLength(10)
                        .HasColumnType("character varying(10)")
                        .HasColumnName("user_uid");

                    b.Property<string>("OtherUserUID")
                        .HasMaxLength(10)
                        .HasColumnType("character varying(10)")
                        .HasColumnName("other_user_uid");

                    b.Property<bool>("AllowReceivingMessages")
                        .HasColumnType("boolean")
                        .HasColumnName("allow_receiving_messages");

                    b.Property<bool>("DisableAnimations")
                        .HasColumnType("boolean")
                        .HasColumnName("disable_animations");

                    b.Property<bool>("DisableSounds")
                        .HasColumnType("boolean")
                        .HasColumnName("disable_sounds");

                    b.Property<bool>("IsPaused")
                        .HasColumnType("boolean")
                        .HasColumnName("is_paused");

                    b.Property<byte[]>("Timestamp")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("bytea")
                        .HasColumnName("timestamp");

                    b.HasKey("UserUID", "OtherUserUID")
                        .HasName("pk_client_pairs");

                    b.HasIndex("OtherUserUID")
                        .HasDatabaseName("ix_client_pairs_other_user_uid");

                    b.HasIndex("UserUID")
                        .HasDatabaseName("ix_client_pairs_user_uid");

                    b.ToTable("client_pairs", (string)null);
                });

            modelBuilder.Entity("MareSynchronosShared.Models.FileCache", b =>
                {
                    b.Property<string>("Hash")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("hash");

                    b.Property<long>("Size")
                        .HasColumnType("bigint")
                        .HasColumnName("size");

                    b.Property<byte[]>("Timestamp")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("bytea")
                        .HasColumnName("timestamp");

                    b.Property<DateTime>("UploadDate")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("upload_date");

                    b.Property<bool>("Uploaded")
                        .HasColumnType("boolean")
                        .HasColumnName("uploaded");

                    b.Property<string>("UploaderUID")
                        .HasMaxLength(10)
                        .HasColumnType("character varying(10)")
                        .HasColumnName("uploader_uid");

                    b.HasKey("Hash")
                        .HasName("pk_file_caches");

                    b.HasIndex("UploaderUID")
                        .HasDatabaseName("ix_file_caches_uploader_uid");

                    b.ToTable("file_caches", (string)null);
                });

            modelBuilder.Entity("MareSynchronosShared.Models.ForbiddenUploadEntry", b =>
                {
                    b.Property<string>("Hash")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("hash");

                    b.Property<string>("ForbiddenBy")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("forbidden_by");

                    b.Property<byte[]>("Timestamp")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("bytea")
                        .HasColumnName("timestamp");

                    b.HasKey("Hash")
                        .HasName("pk_forbidden_upload_entries");

                    b.ToTable("forbidden_upload_entries", (string)null);
                });

            modelBuilder.Entity("MareSynchronosShared.Models.Group", b =>
                {
                    b.Property<string>("GID")
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("gid");

                    b.Property<string>("Alias")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)")
                        .HasColumnName("alias");

                    b.Property<bool>("DisableAnimations")
                        .HasColumnType("boolean")
                        .HasColumnName("disable_animations");

                    b.Property<bool>("DisableSounds")
                        .HasColumnType("boolean")
                        .HasColumnName("disable_sounds");

                    b.Property<string>("HashedPassword")
                        .HasColumnType("text")
                        .HasColumnName("hashed_password");

                    b.Property<bool>("InvitesEnabled")
                        .HasColumnType("boolean")
                        .HasColumnName("invites_enabled");

                    b.Property<string>("OwnerUID")
                        .HasColumnType("character varying(10)")
                        .HasColumnName("owner_uid");

                    b.HasKey("GID")
                        .HasName("pk_groups");

                    b.HasIndex("OwnerUID")
                        .HasDatabaseName("ix_groups_owner_uid");

                    b.ToTable("groups", (string)null);
                });

            modelBuilder.Entity("MareSynchronosShared.Models.GroupBan", b =>
                {
                    b.Property<string>("GroupGID")
                        .HasColumnType("character varying(20)")
                        .HasColumnName("group_gid");

                    b.Property<string>("BannedUserUID")
                        .HasColumnType("character varying(10)")
                        .HasColumnName("banned_user_uid");

                    b.Property<string>("BannedByUID")
                        .HasColumnType("character varying(10)")
                        .HasColumnName("banned_by_uid");

                    b.Property<DateTime>("BannedOn")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("banned_on");

                    b.Property<string>("BannedReason")
                        .HasColumnType("text")
                        .HasColumnName("banned_reason");

                    b.HasKey("GroupGID", "BannedUserUID")
                        .HasName("pk_group_bans");

                    b.HasIndex("BannedByUID")
                        .HasDatabaseName("ix_group_bans_banned_by_uid");

                    b.HasIndex("BannedUserUID")
                        .HasDatabaseName("ix_group_bans_banned_user_uid");

                    b.HasIndex("GroupGID")
                        .HasDatabaseName("ix_group_bans_group_gid");

                    b.ToTable("group_bans", (string)null);
                });

            modelBuilder.Entity("MareSynchronosShared.Models.GroupPair", b =>
                {
                    b.Property<string>("GroupGID")
                        .HasColumnType("character varying(20)")
                        .HasColumnName("group_gid");

                    b.Property<string>("GroupUserUID")
                        .HasColumnType("character varying(10)")
                        .HasColumnName("group_user_uid");

                    b.Property<bool>("DisableAnimations")
                        .HasColumnType("boolean")
                        .HasColumnName("disable_animations");

                    b.Property<bool>("DisableSounds")
                        .HasColumnType("boolean")
                        .HasColumnName("disable_sounds");

                    b.Property<bool>("IsModerator")
                        .HasColumnType("boolean")
                        .HasColumnName("is_moderator");

                    b.Property<bool>("IsPaused")
                        .HasColumnType("boolean")
                        .HasColumnName("is_paused");

                    b.Property<bool>("IsPinned")
                        .HasColumnType("boolean")
                        .HasColumnName("is_pinned");

                    b.HasKey("GroupGID", "GroupUserUID")
                        .HasName("pk_group_pairs");

                    b.HasIndex("GroupGID")
                        .HasDatabaseName("ix_group_pairs_group_gid");

                    b.HasIndex("GroupUserUID")
                        .HasDatabaseName("ix_group_pairs_group_user_uid");

                    b.ToTable("group_pairs", (string)null);
                });

            modelBuilder.Entity("MareSynchronosShared.Models.GroupTempInvite", b =>
                {
                    b.Property<string>("GroupGID")
                        .HasColumnType("character varying(20)")
                        .HasColumnName("group_gid");

                    b.Property<string>("Invite")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("invite");

                    b.Property<DateTime>("ExpirationDate")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("expiration_date");

                    b.HasKey("GroupGID", "Invite")
                        .HasName("pk_group_temp_invites");

                    b.HasIndex("GroupGID")
                        .HasDatabaseName("ix_group_temp_invites_group_gid");

                    b.HasIndex("Invite")
                        .HasDatabaseName("ix_group_temp_invites_invite");

                    b.ToTable("group_temp_invites", (string)null);
                });

            modelBuilder.Entity("MareSynchronosShared.Models.LodeStoneAuth", b =>
                {
                    b.Property<decimal>("DiscordId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("numeric(20,0)")
                        .HasColumnName("discord_id");

                    b.Property<string>("HashedLodestoneId")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("hashed_lodestone_id");

                    b.Property<string>("LodestoneAuthString")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("lodestone_auth_string");

                    b.Property<DateTime?>("StartedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("started_at");

                    b.Property<string>("UserUID")
                        .HasColumnType("character varying(10)")
                        .HasColumnName("user_uid");

                    b.HasKey("DiscordId")
                        .HasName("pk_lodestone_auth");

                    b.HasIndex("UserUID")
                        .HasDatabaseName("ix_lodestone_auth_user_uid");

                    b.ToTable("lodestone_auth", (string)null);
                });

            modelBuilder.Entity("MareSynchronosShared.Models.User", b =>
                {
                    b.Property<string>("UID")
                        .HasMaxLength(10)
                        .HasColumnType("character varying(10)")
                        .HasColumnName("uid");

                    b.Property<string>("Alias")
                        .HasMaxLength(15)
                        .HasColumnType("character varying(15)")
                        .HasColumnName("alias");

                    b.Property<bool>("IsAdmin")
                        .HasColumnType("boolean")
                        .HasColumnName("is_admin");

                    b.Property<bool>("IsModerator")
                        .HasColumnType("boolean")
                        .HasColumnName("is_moderator");

                    b.Property<DateTime>("LastLoggedIn")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("last_logged_in");

                    b.Property<byte[]>("Timestamp")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("bytea")
                        .HasColumnName("timestamp");

                    b.HasKey("UID")
                        .HasName("pk_users");

                    b.ToTable("users", (string)null);
                });

            modelBuilder.Entity("MareSynchronosShared.Models.Auth", b =>
                {
                    b.HasOne("MareSynchronosShared.Models.User", "PrimaryUser")
                        .WithMany()
                        .HasForeignKey("PrimaryUserUID")
                        .HasConstraintName("fk_auth_users_primary_user_temp_id");

                    b.HasOne("MareSynchronosShared.Models.User", "User")
                        .WithMany()
                        .HasForeignKey("UserUID")
                        .HasConstraintName("fk_auth_users_user_temp_id1");

                    b.Navigation("PrimaryUser");

                    b.Navigation("User");
                });

            modelBuilder.Entity("MareSynchronosShared.Models.ClientPair", b =>
                {
                    b.HasOne("MareSynchronosShared.Models.User", "OtherUser")
                        .WithMany()
                        .HasForeignKey("OtherUserUID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_client_pairs_users_other_user_temp_id2");

                    b.HasOne("MareSynchronosShared.Models.User", "User")
                        .WithMany()
                        .HasForeignKey("UserUID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_client_pairs_users_user_temp_id3");

                    b.Navigation("OtherUser");

                    b.Navigation("User");
                });

            modelBuilder.Entity("MareSynchronosShared.Models.FileCache", b =>
                {
                    b.HasOne("MareSynchronosShared.Models.User", "Uploader")
                        .WithMany()
                        .HasForeignKey("UploaderUID")
                        .HasConstraintName("fk_file_caches_users_uploader_uid");

                    b.Navigation("Uploader");
                });

            modelBuilder.Entity("MareSynchronosShared.Models.Group", b =>
                {
                    b.HasOne("MareSynchronosShared.Models.User", "Owner")
                        .WithMany()
                        .HasForeignKey("OwnerUID")
                        .HasConstraintName("fk_groups_users_owner_temp_id8");

                    b.Navigation("Owner");
                });

            modelBuilder.Entity("MareSynchronosShared.Models.GroupBan", b =>
                {
                    b.HasOne("MareSynchronosShared.Models.User", "BannedBy")
                        .WithMany()
                        .HasForeignKey("BannedByUID")
                        .HasConstraintName("fk_group_bans_users_banned_by_temp_id5");

                    b.HasOne("MareSynchronosShared.Models.User", "BannedUser")
                        .WithMany()
                        .HasForeignKey("BannedUserUID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_group_bans_users_banned_user_temp_id6");

                    b.HasOne("MareSynchronosShared.Models.Group", "Group")
                        .WithMany()
                        .HasForeignKey("GroupGID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_group_bans_groups_group_temp_id");

                    b.Navigation("BannedBy");

                    b.Navigation("BannedUser");

                    b.Navigation("Group");
                });

            modelBuilder.Entity("MareSynchronosShared.Models.GroupPair", b =>
                {
                    b.HasOne("MareSynchronosShared.Models.Group", "Group")
                        .WithMany()
                        .HasForeignKey("GroupGID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_group_pairs_groups_group_temp_id1");

                    b.HasOne("MareSynchronosShared.Models.User", "GroupUser")
                        .WithMany()
                        .HasForeignKey("GroupUserUID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_group_pairs_users_group_user_temp_id7");

                    b.Navigation("Group");

                    b.Navigation("GroupUser");
                });

            modelBuilder.Entity("MareSynchronosShared.Models.GroupTempInvite", b =>
                {
                    b.HasOne("MareSynchronosShared.Models.Group", "Group")
                        .WithMany()
                        .HasForeignKey("GroupGID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_group_temp_invites_groups_group_gid");

                    b.Navigation("Group");
                });

            modelBuilder.Entity("MareSynchronosShared.Models.LodeStoneAuth", b =>
                {
                    b.HasOne("MareSynchronosShared.Models.User", "User")
                        .WithMany()
                        .HasForeignKey("UserUID")
                        .HasConstraintName("fk_lodestone_auth_users_user_uid");

                    b.Navigation("User");
                });
#pragma warning restore 612, 618
        }
    }
}
