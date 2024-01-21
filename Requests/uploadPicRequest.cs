﻿using System.ComponentModel.DataAnnotations;

namespace BetsTrading_Service.Requests
{
  public class uploadPicRequest
  {
    [Required]
    [StringLength(100, MinimumLength = 25)]
    public string? id { get; set; }

    [Required]
    public string? Profilepic { get; set; }


  }
}
