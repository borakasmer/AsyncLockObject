using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

public class News
{
    public int ID { get; set; }
    public string Title { get; set; }
    public string UrlTitle { get; set; }
    public string Detail { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Image { get; set; }
}