﻿// <auto-generated />
using System;
using MareSynchronosServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace MareSynchronosServer.Migrations
{
    [DbContext(typeof(MareDbContext))]
    [Migration("20220629212721_AddBannedReason")]
    partial class AddBannedReason
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.6")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder, 1L, 1);

            modelBuilder.Entity("MareSynchronosServer.Models.Banned", b =>
                {
                    b.Property<string>("CharacterIdentification")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Reason")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("CharacterIdentification");

                    b.ToTable("BannedUsers", (string)null);
                });

            modelBuilder.Entity("MareSynchronosServer.Models.CharacterData", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<int>("JobId")
                        .HasColumnType("int");

                    b.Property<string>("CharacterCache")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Hash")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("UserId", "JobId");

                    b.ToTable("CharacterData", (string)null);
                });

            modelBuilder.Entity("MareSynchronosServer.Models.ClientPair", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"), 1L, 1);

                    b.Property<bool>("AllowReceivingMessages")
                        .HasColumnType("bit");

                    b.Property<bool>("IsPaused")
                        .HasColumnType("bit");

                    b.Property<string>("OtherUserUID")
                        .HasColumnType("nvarchar(450)");

                    b.Property<byte[]>("Timestamp")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.Property<string>("UserUID")
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("Id");

                    b.HasIndex("OtherUserUID");

                    b.HasIndex("UserUID");

                    b.ToTable("ClientPairs", (string)null);
                });

            modelBuilder.Entity("MareSynchronosServer.Models.FileCache", b =>
                {
                    b.Property<string>("Hash")
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTime>("LastAccessTime")
                        .HasColumnType("datetime2");

                    b.Property<byte[]>("Timestamp")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.Property<bool>("Uploaded")
                        .HasColumnType("bit");

                    b.Property<string>("UploaderUID")
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("Hash");

                    b.HasIndex("UploaderUID");

                    b.ToTable("FileCaches", (string)null);
                });

            modelBuilder.Entity("MareSynchronosServer.Models.ForbiddenUploadEntry", b =>
                {
                    b.Property<string>("Hash")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ForbiddenBy")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Hash");

                    b.ToTable("ForbiddenUploadEntries", (string)null);
                });

            modelBuilder.Entity("MareSynchronosServer.Models.User", b =>
                {
                    b.Property<string>("UID")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("CharacterIdentification")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsAdmin")
                        .HasColumnType("bit");

                    b.Property<string>("SecretKey")
                        .HasColumnType("nvarchar(max)");

                    b.Property<byte[]>("Timestamp")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.HasKey("UID");

                    b.ToTable("Users", (string)null);
                });

            modelBuilder.Entity("MareSynchronosServer.Models.ClientPair", b =>
                {
                    b.HasOne("MareSynchronosServer.Models.User", "OtherUser")
                        .WithMany()
                        .HasForeignKey("OtherUserUID");

                    b.HasOne("MareSynchronosServer.Models.User", "User")
                        .WithMany()
                        .HasForeignKey("UserUID");

                    b.Navigation("OtherUser");

                    b.Navigation("User");
                });

            modelBuilder.Entity("MareSynchronosServer.Models.FileCache", b =>
                {
                    b.HasOne("MareSynchronosServer.Models.User", "Uploader")
                        .WithMany()
                        .HasForeignKey("UploaderUID");

                    b.Navigation("Uploader");
                });
#pragma warning restore 612, 618
        }
    }
}
