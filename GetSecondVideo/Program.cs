using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

// google API & Youtube Data API
using Google.Apis.Services;
using Google.Apis.YouTube.v3;

namespace GetSecondVideo
{
  class Program
  {
    static async Task Main(string[] args)
    {
      const string InputCsvFile = @"ChannelList.csv";
      const string OutputCsvFile = @"VideoList.csv";
      const int listMaxResult = 50;
      const int outputLinesOfChannel = 5;
      int colomnNumber = 0;
      Dictionary<string, ChannelInfo> channelInfomations = new Dictionary<string, ChannelInfo>();
      List<string> channelIdList = new List<string>();

      System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

      // csvを読み込んで名前とチャンネル名を取得する。
      using (StreamReader csvText = new StreamReader(InputCsvFile, System.Text.Encoding.GetEncoding("shift_jis")))
      {
        string line;
        while ((line = csvText.ReadLine()) != null)
        {
          ChannelInfo tmpInfo = new ChannelInfo();
          string[] tmp = line.Split(",");
          tmpInfo.name = tmp[0];
          tmpInfo.channelId = tmp[1].Split("/").Last();

          channelInfomations.Add(tmpInfo.channelId, tmpInfo);
          channelIdList.Add(tmpInfo.channelId);
        }
      }
      File.WriteAllText(OutputCsvFile, "");

      // Youtube Data API初期設定。
      var youtubeService = new YouTubeService(new BaseClientService.Initializer()
      {
        ApiKey = "Input-ApiKey"
      });

      // チャンネル情報取得用のパラメータ設定。
      var channelsListRequest = youtubeService.Channels.List("id, snippet, contentDetails");
      channelsListRequest.MaxResults = listMaxResult;

      // 50チャンネルずつ情報取得。
      for (int i = 0; i < channelIdList.Count(); i += listMaxResult)
      {
        string tmpIds = "";

        for (int j = 0; j < listMaxResult && i + j < channelIdList.Count(); j++)
        {
          tmpIds += $"{channelIdList[i + j]}, ";
        }
        channelsListRequest.Id = tmpIds.Substring(0, tmpIds.Length - 2);
        
        var channelsListResponse = await channelsListRequest.ExecuteAsync();

        foreach (var channelResult in channelsListResponse.Items)
        {
          channelInfomations[channelResult.Id].title = channelResult.Snippet.Title;
          channelInfomations[channelResult.Id].publishDate = DateTime.Parse(channelResult.Snippet.PublishedAt.ToString());
          channelInfomations[channelResult.Id].uploadList = channelResult.ContentDetails.RelatedPlaylists.Uploads;
        }

        await Task.Delay(1000);
      }

      // チャンネル作成日でソート。
      var sortedchannelInfomations = channelInfomations.OrderBy(c => c.Value.publishDate)
                                                       .ToDictionary(key => key.Key, value => value.Value);

      // チャンネル毎のアップロード動画リスト取得。
      foreach (string key in sortedchannelInfomations.Keys)
      {
        colomnNumber++;
        string formatedColomnNumber = String.Format("{0:000}", colomnNumber);

        Console.WriteLine($"{formatedColomnNumber} : {sortedchannelInfomations[key].title} get start");

        // アップロード動画取得用のパラメータ設定。
        var uploadsListRequest = youtubeService.PlaylistItems.List("snippet");
        uploadsListRequest.MaxResults = listMaxResult;
        uploadsListRequest.PlaylistId = sortedchannelInfomations[key].uploadList;

        // アップロード動画取得&文字列整形。
        List<string> lines = new List<string>();
        while (true)
        {
          var uploadsListResponse = await uploadsListRequest.ExecuteAsync();

          foreach (var listResult in uploadsListResponse.Items)
          {
            string line = $"\"{sortedchannelInfomations[key].name}\",";
            line += $"\"{listResult.Snippet.PublishedAt}\",";
            line += $"\"{listResult.Snippet.Title}\",";
            line += @"=HYPERLINK(" + "\"" + @"https://www.youtube.com/watch?v=";
            line += $"{listResult.Snippet.ResourceId.VideoId}\")";

            lines.Add(line);
          }

          // 動画情報をすべて取得したら最初の5件だけcsvに出力する。
          if (string.IsNullOrEmpty(uploadsListResponse.NextPageToken))
          {
            int loopTime = outputLinesOfChannel;
            if (loopTime > lines.Count()) loopTime = lines.Count();

            lines.Reverse();
            for (int i = 0; i < loopTime; i++) File.AppendAllText(OutputCsvFile, lines[i] + Environment.NewLine, System.Text.Encoding.UTF8);

            break;
          }
          // アップロード動画情報の取得が途中の場合は、次のページをパラメータにセットする。
          else
          {
            uploadsListRequest.PageToken = uploadsListResponse.NextPageToken;
          }

          await Task.Delay(1000);
        }

        Console.WriteLine($"{formatedColomnNumber} : {sortedchannelInfomations[key].title} get end");
      }
    }
  }
}
