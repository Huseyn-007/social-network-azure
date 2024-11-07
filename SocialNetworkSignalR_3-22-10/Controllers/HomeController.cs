using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialNetworkSignalR_3_22_10.Data;
using SocialNetworkSignalR_3_22_10.Entities;
using SocialNetworkSignalR_3_22_10.Models;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;

namespace SocialNetworkSignalR_3_22_10.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<CustomIdentityUser> _userManager;
        private readonly SocialNetworkDbContext _context;

        public HomeController(ILogger<HomeController> logger, UserManager<CustomIdentityUser> userManager, SocialNetworkDbContext context)
        {
            _logger = logger;
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            ViewBag.User = user;
            return View();
        }

        public async Task<IActionResult> GetAllUsers()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var myrequests = _context.FriendRequests.Where(r => r.SenderId == user.Id);

            var myfriends = _context.Friends.Where(f => f.OwnId == user.Id || f.YourFriendId == user.Id);

            var users = await _context.Users
                .Where(u => u.Id != user.Id)
                .OrderByDescending(u => u.IsOnline)
                .Select(u => new CustomIdentityUser
                {
                    Id = u.Id,
                    HasRequestPending = (myrequests.FirstOrDefault(r => r.ReceiverId == u.Id && r.Status == "Request") != null),
                    IsFriend = myfriends.FirstOrDefault(f => f.OwnId == u.Id || f.YourFriendId == u.Id) != null,
                    UserName = u.UserName,
                    IsOnline = u.IsOnline,
                    Image = u.Image,
                    Email = u.Email,
                })
                .ToListAsync();
            //foreach (var item in users)
            //{
            //   // var request = 
            //    if (request != null)
            //    {
            //        item.HasRequestPending = true;
            //    }
            //}

            return Ok(users);
        }

        public async Task<IActionResult> SendFollow(string id)
        {
            var sender = await _userManager.GetUserAsync(HttpContext.User);
            var receiverUser = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (receiverUser != null)
            {
                _context.FriendRequests.Add(new FriendRequest
                {
                    Content = $"{sender.UserName} sent friend request at {DateTime.Now.ToLongDateString()}",
                    SenderId = sender.Id,
                    Sender = sender,
                    ReceiverId = id,
                    Status = "Request"
                });

                await _context.SaveChangesAsync();
                return Ok();
            }
            return BadRequest();
        }

        [HttpPost(Name = "AddMessage")]
        public async Task<IActionResult> AddMessage(MessageModel model)
        {
            try
            {
                var chat = await _context.Chats.FirstOrDefaultAsync(c => c.SenderId == model.SenderId &&
                c.ReceiverId == model.ReceiverId || c.SenderId == model.ReceiverId && c.ReceiverId == model.SenderId);
                if (chat != null)
                {
                    var message = new Message
                    {
                        ChatId = chat.Id,
                        Content = model.Content,
                        DateTime = DateTime.Now,
                        IsImage = false,
                        HasSeen = false,
                    };
                    await _context.Messages.AddAsync(message);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
            return Ok();
        }

        public async Task<IActionResult> GetAllMessages(string receiverId, string senderId)
        {
            var chat = await _context.Chats.Include(nameof(Chat.Messages)).FirstOrDefaultAsync(c => c.SenderId == senderId &&
               c.ReceiverId == receiverId || c.SenderId == receiverId && c.ReceiverId == senderId);
            if (chat != null)
            {
                var user = await _userManager.GetUserAsync(HttpContext.User);
                return Ok(new { Messages = chat.Messages, CurrentUserId = user.Id });
            }
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> TakeRequest(string id)
        {
            var current = await _userManager.GetUserAsync(HttpContext.User);
            var request = await _context.FriendRequests.FirstOrDefaultAsync(r => r.SenderId == current.Id && r.ReceiverId == id);
            if (request == null) return NotFound();
            _context.FriendRequests.Remove(request);
            await _context.SaveChangesAsync();
            return Ok();
        }

        public async Task<IActionResult> GetAllRequests()
        {
            var current = await _userManager.GetUserAsync(HttpContext.User);
            var requests = _context.FriendRequests.Where(r => r.ReceiverId == current.Id);
            return Ok(requests);
        }

        public async Task<IActionResult> DeclineRequest(int id, string senderId)
        {
            try
            {
                var current = await _userManager.GetUserAsync(HttpContext.User);
                var request = await _context.FriendRequests.FirstOrDefaultAsync(f => f.Id == id);
                _context.FriendRequests.Remove(request);

                _context.FriendRequests.Add(new FriendRequest
                {
                    Content = $"{current.UserName} declined your friend request at {DateTime.Now.ToLongDateString()} {DateTime.Now.ToShortTimeString()}",
                    SenderId = current.Id,
                    Sender = current,
                    ReceiverId = senderId,
                    Status = "Notification"
                });
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public async Task<IActionResult> AcceptRequest(string userId, string senderId, int requestId)
        {
            var receiverUser = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId);
            var sender = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == senderId);

            if (receiverUser != null)
            {
                _context.FriendRequests.Add(new FriendRequest
                {
                    Content = $"{sender.UserName} accepted friend request at ${DateTime.Now.ToLongDateString()} ${DateTime.Now.ToShortTimeString()}",
                    SenderId = senderId,
                    ReceiverId = receiverUser.Id,
                    Sender = sender,
                    Status = "Notification"
                });

                var request = await _context.FriendRequests.FirstOrDefaultAsync(r => r.Id == requestId);
                _context.FriendRequests.Remove(request);

                _context.Friends.Add(new Friend
                {
                    OwnId = sender.Id,
                    YourFriendId = receiverUser.Id,
                });
                await _userManager.UpdateAsync(receiverUser);
                await _context.SaveChangesAsync();

                return Ok();
            }
            return BadRequest();
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteRequest(int id)
        {
            try
            {
                var request = await _context.FriendRequests.FirstOrDefaultAsync();
                if (request == null) return NotFound();
                _context.FriendRequests.Remove(request);
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpDelete]
        public async Task<IActionResult> UnfollowUser(string id)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var friend = await _context.Friends.FirstOrDefaultAsync(f => f.YourFriendId == user.Id && f.OwnId == id || f.OwnId == user.Id && f.YourFriendId == id);
            if (friend != null)
            {
                _context.Friends.Remove(friend);
                await _context.SaveChangesAsync();
                return Ok();
            }
            return NotFound();

        }

        public async Task<IActionResult> GoChat(string id)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var chat = await _context.Chats.Include(nameof(Chat.Messages)).FirstOrDefaultAsync(c => c.SenderId == user.Id && c.ReceiverId == id || c.ReceiverId == user.Id && c.SenderId == id);
            if (chat == null)
            {
                chat = new Chat
                {
                    ReceiverId = id,
                    SenderId = user.Id,
                    Messages = new List<Message>()
                };

                await _context.Chats.AddAsync(chat);
                await _context.SaveChangesAsync();
            }

            var chats = _context.Chats.Include(nameof(Chat.Receiver)).Where(c => c.SenderId == user.Id || c.ReceiverId == user.Id);


            var chatBlocks = from c in chats
                             let receiver = (user.Id != c.ReceiverId) ? c.Receiver : _context.Users.FirstOrDefault(u => u.Id == c.SenderId)
                             select new Chat
                             {
                                 Messages = c.Messages,
                                 Id = c.Id,
                                 SenderId = c.SenderId,
                                 Receiver = receiver,
                                 ReceiverId = receiver.Id,
                             };

            var result = chatBlocks.ToList().Where(c => c.ReceiverId != user.Id);
            var model = new ChatViewModel
            {
                CurrentUserId = user.Id,
                CurrentReceiver = id,
                CurrentChat = chat,
                Chats = result.Count() == 0 ? chatBlocks : result,
            };

            return View(model);
        }
    }
}
