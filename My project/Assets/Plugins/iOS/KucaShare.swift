import UIKit
import KakaoSDKCommon
import KakaoSDKShare
import KakaoSDKTemplate

// Unity(C#) 에서 [DllImport("__Internal")] 로 부르는 공유 기능.
// - 인스타그램 스토리: 스티커 PNG 를 붙여넣기 판에 올리고 instagram-stories:// 를 연다.
// - 카카오톡: 카카오 공유 SDK 의 피드 카드. 카톡이 없으면 웹 공유 화면을 연다.

public typealias KucaShareCallback = @convention(c) (UnsafePointer<CChar>?) -> Void

private var kakaoReady = false

private func str(_ p: UnsafePointer<CChar>?) -> String { p.map { String(cString: $0) } ?? "" }

private func report(_ cb: KucaShareCallback?, _ error: String?) {
    DispatchQueue.main.async {
        guard let cb = cb else { return }
        if let e = error { e.withCString { cb($0) } } else { cb(nil) }
    }
}

@_cdecl("kuca_instagram_available")
public func kuca_instagram_available() -> Bool {
    guard let url = URL(string: "instagram-stories://share") else { return false }
    return UIApplication.shared.canOpenURL(url)
}

@_cdecl("kuca_share_instagram_story")
public func kuca_share_instagram_story(_ stickerPath: UnsafePointer<CChar>?, _ appId: UnsafePointer<CChar>?,
                                       _ topColor: UnsafePointer<CChar>?, _ bottomColor: UnsafePointer<CChar>?) -> Bool {
    guard let data = FileManager.default.contents(atPath: str(stickerPath)) else { return false }
    let id = str(appId)
    let query = id.isEmpty ? "" : "?source_application=\(id)"
    guard let url = URL(string: "instagram-stories://share\(query)"), UIApplication.shared.canOpenURL(url) else { return false }
    let item: [String: Any] = [
        "com.instagram.sharedSticker.stickerImage": data,
        "com.instagram.sharedSticker.backgroundTopColor": str(topColor),
        "com.instagram.sharedSticker.backgroundBottomColor": str(bottomColor),
    ]
    UIPasteboard.general.setItems([item], options: [.expirationDate: Date().addingTimeInterval(300)])
    UIApplication.shared.open(url, options: [:], completionHandler: nil)
    return true
}

@_cdecl("kuca_share_kakao")
public func kuca_share_kakao(_ appKey: UnsafePointer<CChar>?, _ title: UnsafePointer<CChar>?, _ desc: UnsafePointer<CChar>?,
                             _ imageUrl: UnsafePointer<CChar>?, _ webUrl: UnsafePointer<CChar>?, _ buttonTitle: UnsafePointer<CChar>?,
                             _ done: KucaShareCallback?) {
    NSLog("[KucaShare] kakao share: title=%@ image=%@ web=%@", str(title), str(imageUrl), str(webUrl))
    if !kakaoReady {
        KakaoSDK.initSDK(appKey: str(appKey))
        kakaoReady = true
    }
    guard let web = URL(string: str(webUrl)) else { report(done, "공유할 주소가 잘못됐어요"); return }
    let link = Link(webUrl: web, mobileWebUrl: web)
    let content = Content(title: str(title), imageUrl: URL(string: str(imageUrl)), description: str(desc), link: link)
    let template = FeedTemplate(content: content, buttons: [Button(title: str(buttonTitle), link: link)])

    let available = ShareApi.isKakaoTalkSharingAvailable()
    NSLog("[KucaShare] kakaotalk available=%d", available ? 1 : 0)
    if available {
        ShareApi.shared.shareDefault(templatable: template) { result, error in
            DispatchQueue.main.async {
                if let result = result {
                    NSLog("[KucaShare] open %@", result.url.absoluteString)
                    UIApplication.shared.open(result.url, options: [:]) { ok in
                        NSLog("[KucaShare] opened=%d", ok ? 1 : 0)
                        report(done, ok ? nil : "카카오톡을 열지 못했어요")
                    }
                } else {
                    NSLog("[KucaShare] share error: %@", String(describing: error))
                    report(done, "카카오톡 공유에 실패했어요: \(error.map { String(describing: $0) } ?? "알 수 없는 오류")")
                }
            }
        }
    } else if let url = ShareApi.shared.makeDefaultUrl(templatable: template) {
        // 카카오톡이 없으면 브라우저의 카카오 공유 화면으로
        NSLog("[KucaShare] web sharer %@", url.absoluteString)
        UIApplication.shared.open(url, options: [:], completionHandler: nil)
        report(done, nil)
    } else {
        report(done, "카카오톡 공유 화면을 열 수 없어요")
    }
}
