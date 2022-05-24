﻿using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AutoMapper;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using vetcommunity.Data;
using vetcommunity.Data.Entities;
using vetcommunity.DTOs.Request;
using vetcommunity.DTOs.Response;
using vetcommunity.Enums;
using vetcommunity.Resources;
using vetcommunity.Services;

namespace vetcommunity.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserManager<User> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly IConfiguration configuration;
        private readonly IMapper mapper;
        private readonly DataContext dataContext;
        private readonly IMailService mailService;

        public UserController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration,
            IMapper mapper, DataContext dataContext, IMailService mailService)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.configuration = configuration;
            this.mapper = mapper;
            this.dataContext = dataContext;
            this.mailService = mailService;
        }

        [HttpPost("Login")]
        public async Task<ActionResult<Response<LoginResponse>>> LoginAsync(LoginRequest loginRequest)
        {
            var user = await userManager.FindByNameAsync(loginRequest.Email);

            if (user == null)
                return NotFound(new Response<LoginResponse>
                {
                    Success = false,
                    Message = Messages.UserNotFound
                });

            if (!await userManager.CheckPasswordAsync(user, loginRequest.Password))
                return Unauthorized(new Response<LoginResponse>
                {
                    Success = false,
                    Message = Messages.UserNotFound
                });

            var userRoles = await userManager.GetRolesAsync(user);

            var authClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                };

            foreach (var userRole in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, userRole));
            }

            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:Key"]));

            var token = new JwtSecurityToken(
                issuer: configuration["JWT:Issuer"],
                audience: configuration["JWT:Audience"],
                expires: DateTime.Now.AddDays(30),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                );

            return new Response<LoginResponse>
            {
                Result = new LoginResponse
                {
                    Token = new JwtSecurityTokenHandler().WriteToken(token),
                    Expiration = token.ValidTo,
                    Id = user.Id,
                    Roles = userRoles,
                    User = mapper.Map<UserResponse>(user)
                }
            };
        }

        [HttpPost("Register")]
        public async Task<ActionResult<Response>> RegisterAsync(RegisterRequest registerRequest)
        {
            var userExists = await userManager.FindByEmailAsync(registerRequest.Email);

            if (userExists != null)
                return NotFound(new Response<LoginResponse>
                {
                    Success = false,
                    Message = Messages.UserAlreadyExists
                });


            User user = new User()
            {
                FullName = registerRequest.FullName,
                UserName = registerRequest.Email,
                Email = registerRequest.Email,
                IsVeterinary = registerRequest.IsVeterinary,
                SecurityStamp = Guid.NewGuid().ToString(),
            };

            var result = await userManager.CreateAsync(user, registerRequest.Password);

            if (registerRequest.IsVeterinary)
            {
                await userManager.AddToRoleAsync(user, UserRole.Vet.ToString());
            }
            else
            {
                await userManager.AddToRoleAsync(user, UserRole.Normal.ToString());
            }

            if (!result.Succeeded)
                return NotFound(new Response<LoginResponse>
                {
                    Success = false,
                    Message = Messages.ErrorCreatingUser
                });

            return new Response { Success = true, Message = "Usuario creado exitosamente" };
        }

        [HttpGet("GetOtp")]
        public async Task<ActionResult<Response>> GenerateOtpAsync([FromQuery] OtpRequest otpRequest)
        {
            User user = await userManager.FindByNameAsync(otpRequest.Email);

            if (user == null)
                return BadRequest(new Response
                {
                    Success = false,
                    Message = Messages.EmailNoRegistered
                });


            Random random = new Random();
            int otpNumber = random.Next(100000, 999999);

            OtpCode otpCode = new OtpCode
            {
                ExpireDate = DateTime.Now.AddMinutes(15).ToUniversalTime(),
                GenerationDate = DateTime.UtcNow,
                Otp = otpNumber.ToString(),
                User = user
            };

            await dataContext.OtpCodes.AddAsync(otpCode);

            await dataContext.SaveChangesAsync();

            mailService.SendOtpMail(otpNumber.ToString(), otpRequest.Email);

            BackgroundJob.Schedule(() => DeleteOtpRecord(otpCode.Id), TimeSpan.FromMinutes((otpCode.ExpireDate - DateTime.UtcNow).TotalMinutes));

            return Created(string.Empty, new Response());
        }

        [HttpPost("ValidateOtp")]
        public async Task<ActionResult<Response>> GenerateOtpAsync(ValidateOtpRequest validateOtpRequest)
        {
            User user = await userManager.FindByNameAsync(validateOtpRequest.Email);

            if (user == null)
                return BadRequest(new Response
                {
                    Success = false,
                    Message = Messages.EmailNoRegistered
                });

            OtpCode otpCode = await dataContext.OtpCodes.OrderByDescending(otp => otp.GenerationDate).FirstOrDefaultAsync(otpCode => otpCode.UserId == user.Id);

            if (otpCode == null)
                return new Response
                {
                    Success = false,
                    Message = Messages.OtpExpired
                };

            if (otpCode.Otp != validateOtpRequest.Otp)
                return new Response
                {
                    Success = false,
                    Message = Messages.OtpInvalid
                };

            return new Response();
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task DeleteOtpRecord(int id)
        {
            OtpCode otpCode = await dataContext.OtpCodes.FindAsync(id);
            dataContext.OtpCodes.Remove(otpCode);
            await dataContext.SaveChangesAsync();
        }
    }
}

